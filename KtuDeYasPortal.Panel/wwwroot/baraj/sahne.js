const add = (a, b) => a.map((v, i) => v + b[i]);
const sub = (a, b) => a.map((v, i) => v - b[i]);
const mul = (a, n) => a.map(v => v * n);
const dot = (a, b) => a.reduce((s, v, i) => s + v * b[i], 0);
const cross = (a, b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
const unit = a => mul(a, 1 / (Math.hypot(...a) || 1));
const clamp = (x, min, max) => Math.max(min, Math.min(max, x));
const toe = (profile, y) => profile.toeZ + (profile.crestZ - profile.toeZ) * y / profile.top;
const upstream = (profile, y) => profile.upstreamToeZ + (profile.upstreamZ - profile.upstreamToeZ) * y / profile.top;

export function inside(profile, [x, y, z]) {
  const e = .001;
  return x >= profile.minX-e && x <= profile.maxX+e && y >= -e && y <= profile.top+e &&
    z >= upstream(profile,y)-e && z <= toe(profile, y)+e;
}

export function buildVolumeGrid(profile) {
  const marks=(low,high,count)=>Array.from({length:count-1},(_,i)=>low+(high-low)*(i+1)/count);
  const columns=marks(profile.minX,profile.maxX,8);
  const depthStations=marks(0,1,9);
  const depthAt=(y,station)=>upstream(profile,y)+(toe(profile,y)-upstream(profile,y))*station;
  const levels=marks(0,profile.top,6).map(y=>{
    const near=upstream(profile,y), far=toe(profile,y);
    const depths=depthStations.map(station=>depthAt(y,station));
    return { y, nodes:columns.flatMap(x=>depths.map(z=>[x,y,z])),
      lines:[
        ...columns.flatMap(x=>[x,y,near,x,y,far]),
        ...depths.flatMap(z=>[profile.minX,y,z,profile.maxX,y,z]),
        profile.minX,y,near,profile.maxX,y,near, profile.minX,y,far,profile.maxX,y,far
      ] };
  });
  const ribs=[profile.minX,...columns,profile.maxX].flatMap(x=>[0,...depthStations,1].flatMap(station=>
    [x,0,depthAt(0,station),x,profile.top,depthAt(profile.top,station)]));
  const ring=y=>[[profile.minX,y,upstream(profile,y)],[profile.maxX,y,upstream(profile,y)],
    [profile.maxX,y,toe(profile,y)],[profile.minX,y,toe(profile,y)]];
  const base=ring(0), crest=ring(profile.top);
  const edges=points=>points.flatMap((p,i)=>[...p,...points[(i+1)%points.length]]);
  const outline=[...edges(base),...edges(crest),...base.flatMap((p,i)=>[...p,...crest[i]])];
  return {levels, ribs, outline};
}

// This reader handles the embedded, unindexed mesh format produced for this scene.
export function readDamGlb(bytes) {
  const header = new DataView(bytes);
  if (header.getUint32(0, true) !== 0x46546c67 || header.getUint32(4, true) !== 2 ||
      header.getUint32(8, true) !== bytes.byteLength) throw Error('Geçersiz GLB dosyası.');
  const jsonSize = header.getUint32(12, true);
  const model = JSON.parse(new TextDecoder().decode(new Uint8Array(bytes, 20, jsonSize)));
  const bin = 28 + jsonSize;
  if (!model.extras?.profile || !model.extras.modelId) throw Error('Modelde baraj kesit bilgisi yok.');
  function floats(index) {
    const accessor = model.accessors[index], view = model.bufferViews[accessor.bufferView];
    if (accessor.componentType !== 5126 || accessor.type !== 'VEC3' || view.byteStride || view.buffer !== 0) {
      throw Error('Bu modelin geometri düzeni desteklenmiyor.');
    }
    return new Float32Array(bytes, bin + (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0), accessor.count * 3);
  }
  const parts = model.scenes[model.scene].nodes.flatMap(index => {
    const node = model.nodes[index];
    return model.meshes[node.mesh].primitives.map(part => {
      const material = model.materials[part.material];
      if (part.mode !== 4 || part.indices !== undefined) throw Error('Bu modelin üçgen düzeni desteklenmiyor.');
      return {
        name: node.name, color: material.pbrMetallicRoughness.baseColorFactor.slice(0, 3),
        style: ['water','river'].includes(material.name) ? 1 : material.name.startsWith('rock') ? 2 :
          material.name.startsWith('concrete') ? 3 : material.name.startsWith('riprap') ? 5 : 0,
        positions: floats(part.attributes.POSITION), normals: floats(part.attributes.NORMAL)
      };
    });
  });
  return { parts, ...model.extras };
}

const vertexSource = `attribute vec3 aPosition;
attribute vec3 aNormal;
uniform vec3 uEye, uForward, uRight, uUp;
uniform vec2 uLens;
varying vec3 vPoint, vNormal;
void main() {
  vec3 offset = aPosition - uEye;
  float z = dot(offset, uForward);
  vPoint = aPosition;
  vNormal = aNormal;
  gl_Position = vec4(dot(offset, uRight)/uLens.x, dot(offset, uUp)/uLens.y, 1.00125*z-1.000625, z);
}`;
const fragmentSource = `precision highp float;
uniform vec3 uColor, uEye;
uniform float uStyle, uOverlay, uShell;
uniform vec4 uGridColor;
varying vec3 vPoint, vNormal;
void main() {
  if (uGridColor.a>0.) { gl_FragColor=uGridColor; return; }
  vec3 n = normalize(vNormal);
  vec3 light = normalize(vec3(-.4,.8,.55));
  float diffuse = max(0., dot(n, light));
  float sky = .49 + .22*(n.y*.5+.5);
  float grain = fract(sin(dot(floor(vPoint*10.), vec3(12.9898, 33.7, 78.233))) * 43758.5453);
  vec3 color = uColor*(sky+diffuse*.47);
  if (uStyle == 1.) {
    float wave = sin(vPoint.z*1.45 + sin(vPoint.x*.11)*1.1)*sin(vPoint.x*.87-vPoint.z*.3);
    float ribbon = pow(max(wave,0.), 10.);
    float fresnel = pow(1.-max(0.,dot(n,normalize(uEye-vPoint))), 3.);
    color = mix(color*(.97+wave*.023), vec3(.18,.38,.48), fresnel*.29+ribbon*.12);
  }
  if (uStyle == 2.) {
    color *= (.93+.16*grain) * (.89+.11*smoothstep(-2.,28.,vPoint.y));
  }
  if (uStyle == 3.) {
    float seam = 1.-smoothstep(.018,.12,abs(mod(vPoint.y+3.3,6.6)-3.3));
    color *= .94+.10*grain-seam*.075;
  }
  if (uStyle == 5.) {
    float row = floor(vPoint.y*1.12);
    vec2 cell = vec2(vPoint.x*.9 + mod(row,2.)*.5,vPoint.y*1.12);
    vec2 f = abs(fract(cell)-.5);
    float stone = 1.-smoothstep(.41,.5,max(f.x,f.y)+min(f.x,f.y)*.24);
    float shade = fract(sin(dot(floor(cell),vec2(12.9898,78.233)))*43758.5453);
    color *= .75 + .32*stone + .19*shade + .12*grain;
  }
  if (uOverlay > 0.) {
    float band = smoothstep(.12,.22,abs(fract((vPoint.x+vPoint.z)*.42)-.5));
    color *= .62+.38*band;
  }
  color=mix(color,vec3(.14,.30,.39),uShell*.72);
  gl_FragColor = vec4(pow(max(color,vec3(0.)),vec3(.4545)),1.-uOverlay*.62-uShell*.78);
}`;

function shaderProgram(gl) {
  const program = gl.createProgram();
  const shaders = [];
  try {
    for (const [type, source] of [[gl.VERTEX_SHADER, vertexSource], [gl.FRAGMENT_SHADER, fragmentSource]]) {
      const shader = gl.createShader(type);
      shaders.push(shader);
      gl.shaderSource(shader, source);
      gl.compileShader(shader);
      if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
      gl.attachShader(program, shader);
    }
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
    return program;
  } catch (error) {
    gl.deleteProgram(program);
    throw error;
  } finally {
    shaders.forEach(shader => gl.deleteShader(shader));
  }
}

function trace(parts, ray, eye) {
  let hit = null;
  let best = Infinity;
  for (const { name, positions } of parts) {
    for (let i = 0; i < positions.length; i += 9) {
      const a = positions.subarray(i,i+3);
      const ab = sub(positions.subarray(i+3,i+6),a);
      const ac = sub(positions.subarray(i+6,i+9),a);
      const t = cross(ray,ac), det = dot(ab,t);
      if (det < 1e-7) continue;
      const offset = sub(eye,a), u = dot(offset,t)/det;
      if (u < 0 || u > 1) continue;
      const q = cross(offset,ab), v = dot(ray,q)/det;
      if (v < 0 || u+v > 1) continue;
      const distance = dot(ac,q)/det;
      if (distance < .5 || distance >= best) continue;
      const point = add(eye,mul(ray,distance));
      best = distance;
      hit = { point, name, distance };
    }
  }
  return hit;
}

function bodyInterval(profile, eye, ray) {
  const slope = (profile.crestZ-profile.toeZ)/profile.top;
  const upSlope = (profile.upstreamZ-profile.upstreamToeZ)/profile.top;
  // Each plane bounds one side of the trapezoidal body by dot(normal, point) <= limit.
  const planes = [
    [[-1,0,0],-profile.minX], [[1,0,0],profile.maxX],
    [[0,-1,0],0], [[0,1,0],profile.top],
    [[0,upSlope,-1],-profile.upstreamToeZ], [[0,-slope,1],profile.toeZ]
  ];
  let front=.5, back=Infinity;
  for (const [normal,limit] of planes) {
    const remaining=limit-dot(normal,eye), speed=dot(normal,ray);
    if (Math.abs(speed)<1e-8) {
      if (remaining<0) return null;
    } else if (speed>0) back=Math.min(back,remaining/speed);
    else front=Math.max(front,remaining/speed);
    if (back<=front) return null;
  }
  return { front, back };
}

export function createStage(canvas, model, callbacks, grid=buildVolumeGrid(model.profile)) {
  const gl = canvas.getContext('webgl', { alpha: true, antialias: true });
  if (!gl) throw Error('Bu tarayıcıda WebGL kullanılamıyor.');
  const program = shaderProgram(gl);
  const position = gl.getAttribLocation(program, 'aPosition');
  const normal = gl.getAttribLocation(program, 'aNormal');
  const uniforms = Object.fromEntries(['Eye','Forward','Right','Up','Lens','Color','Style','Overlay','Shell','GridColor']
    .map(key => [key, gl.getUniformLocation(program, 'u'+key)]));
  const buffers = [];
  function buffer(data, usage = gl.STATIC_DRAW) {
    const id = gl.createBuffer();
    buffers.push(id);
    gl.bindBuffer(gl.ARRAY_BUFFER,id); gl.bufferData(gl.ARRAY_BUFFER,data,usage);
    return id;
  }
  const parts = model.parts.map(part => ({ ...part, vertex: buffer(part.positions), normal: buffer(part.normals) }));
  const isShell = ({name}) => name.startsWith('Govde_') || ['Kret_Yolu','Kret_Yol_Cizgileri','Mansap_DSI_Tas_Yazisi'].includes(name);
  const isSlope = ({name}) => name.startsWith('Yamac_');
  const slopes = parts.filter(isSlope);
  const interiorView = parts.filter(part => !isShell(part) && !isSlope(part));
  const isGallery = part => part.name.startsWith('Galeri_');
  const gallery = parts.filter(isGallery);
  const context = interiorView.filter(part => !isGallery(part));
  const gridShell=parts.filter(part=>isShell(part)&&part.name!=='Mansap_DSI_Tas_Yazisi');
  const lineBuffer=lines=>({ id:buffer(new Float32Array(lines)), count:lines.length/3 });
  const gridLines=grid.levels.map(level=>lineBuffer(level.lines));
  const ribs=lineBuffer(grid.ribs), outline=lineBuffer(grid.outline);
  let activeLevel=Math.floor(grid.levels.length/2);
  const listeners = new AbortController();
  const listen = (name, callback, options) => canvas.addEventListener(name,callback,{ ...options, signal:listeners.signal });
  let width = 1, height = 1, angle = -.42, elevation = .53, zoom = 1, side = false, mode = 'outside';
  let eye, forward, right, up, lens, pointer, frame;
  let pan = [0,0,0];
  function invalidate() { if (!frame) frame = requestAnimationFrame(draw); }
  function paint(drawParts, overlay, shell=0) {
    gl.uniform4f(uniforms.GridColor,0,0,0,0);
    gl.uniform1f(uniforms.Overlay,overlay?1:0);
    gl.uniform1f(uniforms.Shell,shell);
    for (const part of drawParts) {
      gl.uniform3fv(uniforms.Color,overlay?[.64,.45,.20]:part.color);
      gl.uniform1f(uniforms.Style,part.style);
      gl.bindBuffer(gl.ARRAY_BUFFER,part.vertex); gl.vertexAttribPointer(position,3,gl.FLOAT,false,0,0);
      gl.bindBuffer(gl.ARRAY_BUFFER,part.normal); gl.vertexAttribPointer(normal,3,gl.FLOAT,false,0,0);
      gl.drawArrays(gl.TRIANGLES,0,part.positions.length/3);
    }
  }
  function paintLines(batch,color) {
    gl.uniform4fv(uniforms.GridColor,color);
    gl.bindBuffer(gl.ARRAY_BUFFER,batch.id);
    gl.vertexAttribPointer(position,3,gl.FLOAT,false,0,0);
    gl.drawArrays(gl.LINES,0,batch.count);
  }
  function draw() {
    frame = null;
    if (width < 2 || height < 2) return;
    const aspect = width/height;
    const dist = Math.max(379,535/aspect) * zoom;
    const target = add([0,24,-4],pan);
    eye = add(target,mul([Math.cos(elevation)*Math.sin(angle),Math.sin(elevation),Math.cos(elevation)*Math.cos(angle)],dist));
    forward = unit(sub(target,eye));
    right = unit(cross(forward,[0,1,0]));
    up = cross(right,forward);
    lens = [aspect*.31,.31];
    gl.viewport(0,0,canvas.width,canvas.height);
    gl.clearColor(0,0,0,0);
    gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
    gl.enable(gl.DEPTH_TEST); gl.enable(gl.CULL_FACE); gl.useProgram(program);
    gl.uniform3fv(uniforms.Eye,eye); gl.uniform3fv(uniforms.Forward,forward);
    gl.uniform3fv(uniforms.Right,right); gl.uniform3fv(uniforms.Up,up); gl.uniform2fv(uniforms.Lens,lens);
    gl.enableVertexAttribArray(position); gl.enableVertexAttribArray(normal);
    if (mode==='interior') {
      paint(context,false);
      gl.enable(gl.BLEND);
      gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
      gl.depthMask(false);
      paint(slopes,false,.86);
      paint(gridShell,false,1);
      if(callbacks.picking()==='gallery')paint(gallery,true);
      gl.disableVertexAttribArray(normal);
      paintLines(ribs,[.55,.73,.81,.23]);
      gridLines.forEach((batch,i)=>{
        if(i!==activeLevel)paintLines(batch,[.55,.73,.81,.23]);
      });
      paintLines(gridLines[activeLevel],[.48,.89,.94,.76]);
      paintLines(outline,[.77,.89,.95,.95]);
      gl.depthMask(true);
      gl.disable(gl.BLEND);
    } else {
      paint(parts,false);
    }
    callbacks.draw(project, width, height);
  }
  function project(point) {
    if (!eye) return null;
    const q = sub(point,eye), z = dot(q,forward);
    if (z <= .5) return null;
    return [width*(.5+dot(q,right)/(2*z*lens[0])),height*(.5-dot(q,up)/(2*z*lens[1]))];
  }
  function reset() {
    angle = mode === 'upstream' ? Math.PI-.40 : mode==='interior'?(side?1.32:-.62):-.34;
    elevation = mode==='interior'?(side?.29:.51):.60;
    zoom = mode==='interior'?.83:1;
    pan = [0,0,0];
    invalidate();
  }
  const resize = new ResizeObserver(() => {
    const rect = canvas.getBoundingClientRect(), dpr = Math.min(2,window.devicePixelRatio || 1);
    width = rect.width; height = rect.height;
    canvas.width = Math.max(1,Math.round(width*dpr)); canvas.height = Math.max(1,Math.round(height*dpr));
    invalidate();
  });
  resize.observe(canvas);
  listen('pointerdown', e => {
    if (pointer || (e.button !== 0 && e.button !== 2)) return;
    canvas.setPointerCapture(e.pointerId);
    pointer = { id:e.pointerId, x:e.clientX, y:e.clientY, moved:false, pan:e.button === 2 || e.shiftKey };
  });
  listen('pointermove', e => {
    if (!pointer || pointer.id !== e.pointerId) return;
    const dx = e.clientX-pointer.x, dy = e.clientY-pointer.y;
    if (!pointer.moved && Math.hypot(dx,dy)<4) return;
    if (pointer.pan && right) {
      const speed = 155*zoom/Math.max(1,height);
      pan = add(pan,add(mul(right,-dx*speed),mul(up,dy*speed)));
    } else {
      angle -= dx*.008; elevation = clamp(elevation+dy*.007,.05,1.46);
    }
    pointer = { ...pointer, x:e.clientX, y:e.clientY, moved:true };
    invalidate();
  });
  listen('pointerup', e => {
    if (!pointer || e.pointerId !== pointer.id) return;
    const pick = !pointer.moved && !pointer.pan;
    pointer = null;
    const picking = callbacks.picking();
    if (!pick || !picking || !eye) return;
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX-rect.left)/width*2-1, y = 1-(e.clientY-rect.top)/height*2;
    const ray = unit(add(forward,add(mul(right,x*lens[0]),mul(up,y*lens[1]))));
    if (mode==='interior' && picking==='volume') {
      const range=bodyInterval(model.profile,eye,ray);
      const obstruction=trace(context,ray,eye);
      const far = Math.min(range?.back??0,obstruction?.distance??Infinity);
      if (!range || far-range.front<.01) {
        callbacks.pick(null);
      } else {
        callbacks.pick({ range:[add(eye,mul(ray,range.front)),add(eye,mul(ray,far))] });
      }
      return;
    }
    if (mode==='interior' && picking==='gallery') {
      callbacks.pick(trace(interiorView,ray,eye));
      return;
    }
    callbacks.pick(trace(parts,ray,eye));
  });
  listen('pointercancel', () => { pointer = null; });
  listen('contextmenu', e => e.preventDefault());
  listen('wheel', e => {
    e.preventDefault();
    zoom = clamp(zoom*Math.exp(e.deltaY*.0011),.20,2.5);
    invalidate();
  }, { passive:false });
  invalidate();
  return {
    invalidate, reset,
    setView(value) { mode=value; reset(); },
    setSide(value) { side=value; reset(); },
    setGridLevel(value) { activeLevel=value; invalidate(); },
    dispose() {
      listeners.abort();
      resize.disconnect();
      if (frame) cancelAnimationFrame(frame);
      buffers.forEach(id => gl.deleteBuffer(id)); gl.deleteProgram(program);
    }
  };
}

export function frustum(position, target, angle) {
  const direction = unit(sub(target,position));
  const right = unit(cross(direction,Math.abs(direction[1])>.98?[1,0,0]:[0,1,0]));
  const up = cross(right,direction);
  const length = Math.min(58,Math.hypot(...sub(target,position)));
  const center = add(position,mul(direction,length));
  const width = Math.tan(angle*Math.PI/360)*length;
  return [[-1,-1],[1,-1],[1,1],[-1,1]].map(([x,y]) => add(center,add(mul(right,x*width),mul(up,y*width*9/16))));
}
