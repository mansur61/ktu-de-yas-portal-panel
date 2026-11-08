import { buildVolumeGrid, createStage, frustum, inside, readDamGlb } from './sahne.js';

const instances = new WeakMap();
const types = {
  surface: { name: 'Yüzey sensörü', short: 'Yüzey', icon: '●' },
  internal: { name: 'Gövde / galeri sensörü', short: 'Gövde / galeri', icon: '◉' },
  camera: { name: 'Kamera', short: 'Yamaç kamerası', icon: '◆' }
};
const ui = `
<style>
:host{display:block;color-scheme:dark;--bg:#0d1722;--panel:#142130;--line:#2b3e50;--ink:#edf4f7;--muted:#a9bac8;--cyan:#9fe1f0;--surface:#f7c77c;--internal:#e4b4ff;--camera:#93e2f4;color:var(--ink);font:14px/1.48 system-ui,sans-serif}
*,*::before,*::after{box-sizing:border-box}[hidden]{display:none!important}
h1,h2,h3,p{margin:0}button,input,select{font:inherit;color:inherit}
button{padding:7px 11px;border:1px solid var(--line);border-radius:7px;background:#1b2b3b;line-height:1.3;cursor:pointer}
button:hover{background:#273f52}button:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid #f6c77f;outline-offset:2px}
button[aria-pressed=true]{background:#284453;border-color:#658e9e;color:#e9faff}
button.primary{background:#a3e3ef;border-color:#a3e3ef;color:#122933;font-weight:700}
.dashboard{border:1px solid var(--line);border-radius:16px;background:var(--bg);overflow:hidden}
.header{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;padding:18px 21px;border-bottom:1px solid var(--line)}
.eyebrow{font-size:11px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--cyan);margin-bottom:3px}
h1{font-size:clamp(22px,3vw,34px);font-weight:650;letter-spacing:-.037em;line-height:1.15}
h2{font-size:18px;letter-spacing:-.015em}h3{font-size:14px}
small,.muted{color:var(--muted)}small{font-size:12px}
.layout{display:grid;grid-template-columns:minmax(0,1fr) minmax(265px,310px)}
.scene{min-width:0}
.toolbar,.foot{display:flex;align-items:center;flex-wrap:wrap;gap:7px;padding:10px 13px;background:#101e2b}
.toolbar{border-bottom:1px solid var(--line)}.foot{border-top:1px solid var(--line);justify-content:space-between}
.toolbar label{display:flex;align-items:center;gap:6px;font-size:12px;color:var(--muted)}
input[type=range]{width:clamp(72px,12vw,145px);accent-color:var(--cyan);padding:0}
.stage{position:relative;overflow:hidden;background:radial-gradient(ellipse at 47% 38%,#314657,#172937 63%,#11212e);height:clamp(355px,66vh,685px)}
.stage canvas,.over,.rays{width:100%;height:100%;position:absolute;inset:0}
canvas{display:block;touch-action:none;cursor:grab}canvas:active{cursor:grabbing}canvas.pick{cursor:crosshair}
.over,.rays{pointer-events:none}
.rays{overflow:hidden;color:var(--camera)}.rays path{fill:none;stroke:currentColor;stroke-width:1.4;stroke-dasharray:5 4;opacity:.86}
.pin{position:absolute;display:flex;align-items:center;gap:5px;pointer-events:auto;background:#0b1b28f2;padding:4px 7px;border-color:#7d9baa;color:var(--tag);font-size:11px;white-space:nowrap}
.pin .pinName{color:var(--ink);max-width:138px;overflow:hidden;text-overflow:ellipsis}
.point{position:absolute;width:12px;height:12px;border:2px solid #122430;border-radius:50%;background:var(--tag);box-shadow:0 0 0 1px #f6ffff;transform:translate(-50%,-50%);pointer-events:none}
.point[data-kind=camera]{border-radius:2px;transform:translate(-50%,-50%) rotate(45deg)}
.leaders{position:absolute;inset:0;width:100%;height:100%;pointer-events:none;overflow:hidden}
.leaders path{stroke:#e4edf3;stroke-width:1.15;fill:none;opacity:.88}.leaders .inside{stroke-dasharray:3 3}
.leaders .gridX,.leaders .gridY,.leaders .gridZ{stroke-width:2.3;stroke-dasharray:5 3;opacity:1}
.gridX,.leaders .gridX{stroke:#ffe199;color:#ffe199}.gridY,.leaders .gridY{stroke:#b7f3bc;color:#b7f3bc}.gridZ,.leaders .gridZ{stroke:#9ceaff;color:#9ceaff}
.gridKey{position:absolute;top:32px;left:12px;display:flex;gap:5px 10px;flex-wrap:wrap;background:#112331e8;border-radius:5px;padding:3px 7px;font-size:11px;pointer-events:none}
.gridNode{position:absolute;width:14px;height:14px;padding:0;border-radius:50%;transform:translate(-50%,-50%);background:#b2f5fc;border:3px solid #13313e;box-shadow:0 0 0 1px #b2f5fc;pointer-events:auto}
.gridNode:hover,.gridNode:focus-visible{background:#fff1b6;box-shadow:0 0 0 3px #ffe199}
.toolbar select{width:auto;min-width:76px}
.toolbar label{white-space:nowrap}
.positionPanel{display:grid;gap:9px;background:#12202d;border-top:1px solid var(--line);padding:12px 13px}
.positionFacts{color:var(--muted);font-size:12px;font-variant-numeric:tabular-nums}
.positionViews figure{margin:0;padding:8px 10px;border:1px solid var(--line);border-radius:8px;background:var(--bg)}
.positionViews figcaption{font-weight:650;font-size:12px}
.positionViews svg{display:block;width:100%;height:clamp(138px,13vw,175px)}
.positionViews text{font:12px system-ui,sans-serif;fill:var(--muted)}
.positionViews .body{fill:#394d58;stroke:#a2b7c3;stroke-width:1.35}
.positionViews .road{fill:#70838e;stroke:#a2b7c3;stroke-width:1}
.positionViews .measure{fill:none;stroke:var(--internal);stroke-width:1.7;stroke-dasharray:4 3}
.positionViews .spot{fill:var(--internal);stroke:#fff;stroke-width:1.5}
.pin[aria-pressed=true]{border-color:var(--tag);color:var(--tag);background:#172d40}
[data-kind=surface]{--tag:var(--surface)}[data-kind=internal]{--tag:var(--internal)}[data-kind=camera]{--tag:var(--camera)}
.stage.picking .pin{pointer-events:none}
.draftPin{position:absolute;pointer-events:none;transform:translate(-50%,-50%);width:15px;height:15px;border:2px solid #fff;border-radius:50%;background:var(--internal);box-shadow:0 0 0 4px #192531bb}
.legend{position:absolute;top:10px;left:12px;display:flex;gap:7px 13px;flex-wrap:wrap;font-size:11px;color:var(--muted);pointer-events:none}
.legend b,.icon{color:var(--tag);font-weight:700}
.axes{position:absolute;left:13px;bottom:12px;pointer-events:none;color:#c0d0d8;font-size:11px;line-height:1.3;background:#112331d9;padding:5px 7px;border-radius:4px}
.aside{background:var(--panel);border-left:1px solid var(--line);min-width:0;padding:13px;display:grid;align-content:start;gap:11px}
.between{display:flex;justify-content:space-between;align-items:center;gap:7px;flex-wrap:wrap}
.items{display:grid;gap:5px;max-height:330px;overflow:auto}
.aside:has(.form:not([hidden])) .items{max-height:218px}
.device{display:grid;grid-template-columns:14px minmax(0,1fr) max-content;gap:7px;text-align:left;align-items:center;padding:6px 8px}
.device strong{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:12px;font-weight:600}
.device .kind{display:block;font-size:11px;color:var(--muted)}.device .id{color:var(--tag);font:11px ui-monospace,monospace}
.form{display:grid;gap:7px;border-top:1px solid var(--line);padding-top:10px}
label{display:block;font-size:12px}
input:not([type=range]),select{width:100%;min-width:0;border:1px solid var(--line);border-radius:5px;background:#0d1824;padding:5px 6px}
fieldset{padding:6px;margin:0;border:1px solid var(--line);border-radius:6px;min-width:0}
legend{font-size:11px;color:var(--muted);padding:0 3px}
.xyz{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:5px}
.actions{display:flex;gap:6px;flex-wrap:wrap}
.status{min-height:1.5em;display:block;overflow-wrap:anywhere}
.state{color:var(--cyan);font-size:11px;border:1px solid var(--line);border-radius:20px;padding:4px 9px}
.error{padding:14px;color:#ffcfb9}
@media(max-width:830px){.layout{grid-template-columns:minmax(0,1fr)}.aside{border-left:0;border-top:1px solid var(--line)}.stage{height:clamp(325px,56vh,530px)}}
@media(min-width:831px){.scene:has([data-view=interior][aria-pressed=true]):has(.positionPanel:not([hidden])) .stage{height:clamp(345px,48vh,540px)}}
@media(max-width:1160px){.pin .pinName{display:none}}
@media(max-width:430px){.header{padding:13px}.toolbar,.foot{padding:8px}.stage{height:335px}}
</style>
<section class="dashboard" aria-label="Baraj dijital ikizi">
  <header class="header">
    <div><p class="eyebrow">Dijital ikiz · Sensör yerleşimi</p><h1>Atasu Barajı</h1></div>
    <span class="state">Fotoğraf referanslı model</span>
  </header>
  <p id="loading" class="error" role="status">3B model hazırlanıyor…</p>
  <div id="layout" class="layout" hidden>
    <section class="scene" aria-label="3B sahne">
      <div class="toolbar" aria-label="Sahne görünümleri">
        <button type="button" data-view="outside" aria-pressed="true">Mansap</button>
        <button type="button" data-view="upstream" aria-pressed="false">Memba</button>
        <button type="button" data-view="interior" aria-pressed="false">İç sensörler</button>
        <button type="button" id="sideView" aria-pressed="false" hidden>Yandan bak</button>
        <label id="gridField" hidden>Kot katmanı <select id="gridLevel"></select></label>
        <button type="button" id="reset">Sığdır</button>
      </div>
      <div class="stage">
        <canvas id="canvas" role="img" aria-label="Baraj, iki yamaç, rezervuar ve gövde galerisi"></canvas>
        <svg class="rays" aria-hidden="true"><path id="rayPath"/></svg>
        <svg class="leaders" aria-hidden="true"><path id="leaders"/><path id="internalLeaders" class="inside"/><path id="gridX" class="gridX"/><path id="gridY" class="gridY"/><path id="gridZ" class="gridZ"/></svg>
        <div id="markers" class="over"></div>
        <div id="gridNodes" class="over" aria-label="Seçilebilir ızgara kesişimleri" hidden></div>
        <span id="draftPin" class="draftPin" aria-hidden="true" hidden></span>
        <div class="legend" aria-label="İşaretler">
          <span data-kind="surface"><b>●</b> Yüzey</span><span data-kind="internal"><b>◉</b> Dolgu / temsili galeri</span><span data-kind="camera"><b>◆</b> Kamera</span>
        </div>
        <div id="gridKey" class="gridKey" hidden><span class="gridX">X · Kret</span><span class="gridY">Y · Kot</span><span class="gridZ">Z · Derinlik</span></div>
        <div class="axes">X · Kret &nbsp; Y · Kot &nbsp; Z · Mansap</div>
      </div>
      <div class="foot"><small id="hint">Sürükle: döndür · Tekerlek: yakınlaş · Sağ sürükle: kaydır</small><small>Yerel koordinatlar</small></div>
      <section id="positionPanel" class="positionPanel" aria-label="İç sensörün konumu" hidden>
        <h3 id="positionTitle">İç sensör konumu</h3>
        <p id="positionFacts" class="positionFacts">Gövdeye veya galeriye tıklayın. Seçtiğiniz nokta üstten görünümde gösterilir.</p>
        <div id="positionViews" class="positionViews" hidden>
          <figure>
            <figcaption>Üstten · X ve Z</figcaption>
            <svg id="plan" viewBox="0 0 330 206" role="img" aria-label="Sensörün kret boyunca ve memba ile mansap arasındaki yeri">
              <text x="165" y="15" text-anchor="middle">Memba</text>
              <rect id="planBody" class="body"/><rect id="planRoad" class="road"/>
              <path id="planCut" class="measure"/><circle id="planSpot" class="spot" r="5"/>
              <text x="165" y="201" text-anchor="middle">Mansap</text>
            </svg>
          </figure>
        </div>
      </section>
    </section>
    <aside class="aside" aria-label="Cihazlar">
      <div class="between"><h2>Cihazlar</h2><button class="primary" id="new" type="button">+ Yeni cihaz</button></div>
      <div class="actions"><button id="addInternal" type="button">+ İç sensör ekle</button></div>
      <div id="items" class="items" aria-label="Cihaz listesi"></div>
      <form id="form" class="form" autocomplete="off" hidden>
        <div class="between"><h3 id="formTitle">Yeni cihaz</h3><button id="close" type="button">Kapat</button></div>
        <label>Cihaz adı <input id="name" required maxlength="90"></label>
        <label>Tür <select id="type"><option value="surface">Yüzey sensörü</option><option value="internal">Gövde / galeri sensörü</option><option value="camera">Kamera</option></select></label>
        <fieldset><legend>Konum · X Y Z</legend><div class="xyz">
          <label>X<input id="px" type="number" step="any" required></label><label>Y<input id="py" type="number" step="any" required></label><label>Z<input id="pz" type="number" step="any" required></label>
        </div></fieldset>
        <fieldset id="aimFields" hidden><legend>Bakış hedefi · X Y Z</legend><div class="xyz">
          <label>X<input id="tx" type="number" step="any"></label><label>Y<input id="ty" type="number" step="any"></label><label>Z<input id="tz" type="number" step="any"></label>
        </div><label>Yatay görüş açısı (°)<input id="fov" type="number" min="5" max="150" step="any"></label></fieldset>
        <div class="actions"><button id="pick" type="button" aria-pressed="false">Sahneden konum seç</button><button id="freePick" type="button" aria-pressed="false" hidden>Serbest nokta seç</button><button id="gallery" type="button" aria-pressed="false" hidden>Galeriye tıkla</button><button id="aim" type="button" aria-pressed="false" hidden>Hedef seç</button></div>
        <fieldset id="depthField" hidden><legend>Seçilen doğrultuda derinlik</legend>
          <label>Ön yüzden arkaya · <output id="depthRead">50%</output>
            <input id="depth" type="range" min="0" max="100" step="1" value="50">
          </label>
        </fieldset>
        <div class="actions"><button class="primary" id="save" type="submit">Kaydet</button><button id="remove" type="button" hidden>Sil</button></div>
      </form>
      <div class="actions"><button id="export" type="button">JSON indir</button><button id="import" type="button">JSON aç</button><input id="file" type="file" accept=".json,application/json" aria-label="Cihaz yerleşimi dosyası" hidden></div>
      <small class="status" id="status" role="status">Düzenlemek için bir cihaz seçin.</small>
    </aside>
  </div>
</section>`;

const validVector = v => Array.isArray(v) && v.length === 3 && v.every(n => Number.isFinite(n) && Math.abs(n) <= 10000);
function validate(device, profile) {
  if (!device || typeof device.name !== 'string' || !device.name.trim() || device.name.length > 90 ||
      !Object.hasOwn(types,device.type) || !validVector(device.position)) throw Error('Cihazın adını, türünü ve konumunu kontrol edin.');
  if (device.type === 'internal' && !inside(profile,device.position)) throw Error('Sensör gövde veya galeri sınırları içinde olmalı.');
  if (device.type === 'camera' && (!validVector(device.target) || Math.hypot(...device.target.map((n,i) => n-device.position[i])) < .01 ||
    !Number.isFinite(device.fovDeg) || device.fovDeg < 5 || device.fovDeg > 150)) throw Error('Farklı bir bakış hedefi ve 5–150° arası bir görüş açısı girin.');
}
function readLayout(data, model) {
  if (data?.schemaVersion !== 2 || data.modelId !== model.modelId || !Array.isArray(data.devices)) {
    throw Error('Cihaz dosyası bu baraj modeline ait değil.');
  }
  const seen = new Set();
  return data.devices.map(device => {
    validate(device,model.profile);
    const { id, name, type, position, target, fovDeg } = device;
    if (typeof id !== 'string' || !id.trim() || id.length > 32 || seen.has(id)) throw Error('Cihaz kimlikleri benzersiz olmalı.');
    seen.add(id);
    return { id, name, type, position, ...(type === 'camera' ? { target, fovDeg } : {}) };
  });
}

export async function mount(host, receiver, initialJson, embedded) {
  unmount(host);
  const root = host.shadowRoot ?? host.attachShadow({ mode:'open' });
  root.innerHTML = ui;
  const $ = id => root.getElementById(id);
  const events = new AbortController();
  const storage = !receiver;
  let stage;
  const observer = new MutationObserver(() => { if (!host.isConnected) unmount(host); });
  observer.observe(host.ownerDocument,{childList:true,subtree:true});
  instances.set(host,() => { events.abort(); observer.disconnect(); stage?.dispose(); root.replaceChildren(); });
  const listen = (element,event,callback) => element.addEventListener(event,callback,{signal:events.signal});
  async function asset(file, convert) {
    const response = await fetch(new URL(file,import.meta.url),{signal:events.signal});
    if (!response.ok) throw Error('Dosya açılamadı: '+file);
    return convert(response);
  }
  try {
    const [bytes, initial] = await Promise.all([
      embedded?.model ?? asset('baraj-modeli.bin',r => r.arrayBuffer()),
      initialJson ? JSON.parse(initialJson) : embedded?.layout ?? asset('yerlesim.json',r => r.json())
    ]);
    if (events.signal.aborted) return;
    const model = readDamGlb(bytes), profile = model.profile;
    const grid=buildVolumeGrid(profile);
    let gridIndex=Math.floor(grid.levels.length/2), gridNodes=[];
    const faceZ = y => profile.toeZ+(profile.crestZ-profile.toeZ)*y/profile.top;
    const rangeZ = profile.toeZ-profile.upstreamToeZ, rangeX = profile.maxX-profile.minX;
    const planScale = Math.min(270/rangeX,155/rangeZ);
    const planX = x => 165+(x-(profile.minX+profile.maxX)/2)*planScale;
    const planZ = z => 103+(z-(profile.upstreamToeZ+profile.toeZ)/2)*planScale;
    const attributes = (id,values) => Object.entries(values).forEach(([name,value]) => $(id).setAttribute(name,value));
    attributes('planBody',{x:planX(profile.minX),y:planZ(profile.upstreamToeZ),width:rangeX*planScale,height:rangeZ*planScale});
    attributes('planRoad',{x:planX(profile.minX),y:planZ(profile.upstreamZ),width:rangeX*planScale,height:(profile.crestZ-profile.upstreamZ)*planScale});
    const initialDevices = readLayout(initial,model);
    let devices = initialDevices;
    const storeKey = model.modelId+'/devices';
    if (storage) {
      try {
        const saved = localStorage.getItem(storeKey);
        if (saved) devices = readLayout(JSON.parse(saved),model);
      } catch {}
    }
    let editing = false, selected = null, view = 'outside', mode = null, side = false, depthRange = null;
    let lastPositionKey;
    let pins = [], saveQueue = Promise.resolve(), revision = 0;
    const posKeys = ['px','py','pz'], targetKeys = ['tx','ty','tz'];
    const numbers = keys => keys.map(key => $(key).valueAsNumber);
    const write = (keys, values=[]) => keys.forEach((key,i) => { $(key).value=values[i] ?? ''; });
    const status = message => { $('status').textContent=message; };
    const layout = () => ({ schemaVersion:2, modelId:model.modelId,
      axes:{ x:'kret boyunca mansap görünümünde sağa', y:'yukarı', z:'mansaba' }, devices });
    const saveLayout = async message => {
      const json = JSON.stringify(layout());
      status(message);
      if (storage) {
        try { localStorage.setItem(storeKey,json); }
        catch { status(message+' Kalıcı kopya için JSON indirin.'); }
      } else {
        const version = ++revision;
        saveQueue = saveQueue.then(async () => {
          if (events.signal.aborted) return;
          try { await receiver.invokeMethodAsync('YerlesimDegisti',json); }
          catch {
            if (!events.signal.aborted && version === revision) {
              status('Uygulamaya kayıt iletilemedi. Yerleşimi JSON olarak indirebilirsiniz.');
            }
          }
        });
        await saveQueue;
      }
    };
    const draft = () => ({
      id:selected, name:$('name').value.trim(), type:$('type').value, position:numbers(posKeys),
      ...($('type').value==='camera'?{ target:numbers(targetKeys), fovDeg:$('fov').valueAsNumber }:{})
    });
    function pathFor(project, camera) {
      if (!camera || !validVector(camera.target) || !(camera.fovDeg>=5&&camera.fovDeg<=150)) return '';
      const start = project(camera.position);
      const corners = frustum(camera.position,camera.target,camera.fovDeg).map(project);
      if (!start || corners.some(point => !point)) return '';
      return corners.map(point => 'M'+start.join(' ')+' L'+point.join(' ')).join(' ')+
        ' M'+corners.map(point=>point.join(' ')).join(' L')+' Z';
    }
    const coordinateFormat = new Intl.NumberFormat('tr',{maximumFractionDigits:2});
    const fmt=n=>coordinateFormat.format(n);
    grid.levels.forEach(({y},i)=>$('gridLevel').add(new Option('Y '+fmt(y),i)));
    $('gridLevel').value=gridIndex;
    const interior=()=>view==='interior';
    function locate(input, project) {
      const active=input?.type==='internal' && interior();
      const point=active && validVector(input.position) && inside(profile,input.position) ? input.position : null;
      const surface=point?[point[0],point[1],faceZ(point[1])]:null;
      const a=point&&project(point), b=surface&&project(surface);
      const guides=view==='interior'&&a?[
        project([profile.minX,point[1],point[2]]),project([point[0],0,point[2]]),b
      ]:[null,null,null];
      ['gridX','gridY','gridZ'].forEach((id,i)=>attributes(id,{d:guides[i]?'M'+a.join(' ')+' L'+guides[i].join(' '):''}));

      const key=JSON.stringify([active,view,input?.id,input?.name,point]);
      if(lastPositionKey===key)return;
      lastPositionKey=key;
      $('positionPanel').hidden=!active;
      $('positionViews').hidden=!point;
      $('positionTitle').textContent=input?.id ? input.id+' · '+input.name : 'Yeni iç sensörün konumu';
      if(!point){
        $('positionFacts').textContent='Bir kot ve kesişim noktası seçin. Konum üstten görünümde de gösterilir.';
        return;
      }
      const [x,y,z]=point, front=surface[2];
      $('positionFacts').textContent='X '+fmt(x)+' · Y '+fmt(y)+' · Z '+fmt(z)+
        ' · Mansap yüzüne yatay uzaklık '+fmt(Math.max(0,front-z))+' yerel birim';
      const px=planX(x), pz=planZ(z);
      attributes('planCut',{d:'M'+px+' '+planZ(profile.upstreamToeZ)+' V'+planZ(profile.toeZ)});
      attributes('planSpot',{cx:px,cy:pz});
    }
    function draw(project,width,height) {
      if(mode==='grid'){
        for(const {node,position} of gridNodes){
          const point=project(position);
          node.hidden=!point||point[0]<8||point[0]>width-8||point[1]<8||point[1]>height-8;
          if(!node.hidden){
            node.style.left=point[0]+'px'; node.style.top=point[1]+'px';
          }
        }
      }
      const visible=[];
      for (const pin of pins) {
        const {device,node,anchor}=pin, point=project(device.position);
        anchor.hidden = !point || point[0]<5 || point[0]>width-5 || point[1]<5 || point[1]>height-5 ||
          (device.type==='internal')!==interior();
        node.hidden=anchor.hidden||mode==='grid';
        if(!anchor.hidden&&mode==='grid'){
          anchor.style.left=point[0]+'px'; anchor.style.top=point[1]+'px'; anchor.style.opacity='.65';
        }
        if (!node.hidden) visible.push({...pin,point});
      }
      visible.sort((a,b)=>Number(b.device.id===selected)-Number(a.device.id===selected));
      const boxes=[], leaders=[], internal=[];
      const clamp=(n,low,high)=>Math.max(low,Math.min(high,n));
      for (const {device,node,anchor,point:[x,y]} of visible) {
        const w=node.offsetWidth,h=node.offsetHeight;
        const base = x+w+22<=width ? x+16 : x-w-16;
        const other = base>x ? x-w-16 : x+16;
        const candidates=[[base,y-h-7],[base,y+8],[other,y-h-7],[other,y+8],[base,y-h*2-14],[base,y+h+13],[other,y-h*2-14],[other,y+h+13]];
        const safe=candidates.map(([a,b])=>[clamp(a,5,width-w-5),clamp(b,30,height-h-5)]);
        const box=safe.find(([a,b])=>!boxes.some(([c,d,e,f])=>a<c+e+4&&a+w+4>c&&b<d+f+4&&b+h+4>d)&&
          !visible.some(({point:[px,py]})=>px>=a-4&&px<=a+w+4&&py>=b-4&&py<=b+h+4));
        anchor.style.left=x+'px'; anchor.style.top=y+'px'; anchor.style.opacity='1';
        if (!box) { node.hidden=true; continue; }
        const [left,top]=box;
        boxes.push([left,top,w,h]);
        node.style.left=left+'px'; node.style.top=top+'px';
        const line='M'+x+' '+y+' L'+clamp(x,left,left+w)+' '+clamp(y,top,top+h);
        (device.type==='internal'?internal:leaders).push(line);
      }
      $('leaders').setAttribute('d',leaders.join(' '));
      $('internalLeaders').setAttribute('d',internal.join(' '));
      const input = editing ? draft() : null;
      locate(input,project);
      const saved = devices.find(d=>d.id===selected);
      const candidate = input?.type==='internal' && interior() && validVector(input.position) &&
        (!saved || input.position.some((v,i)=>v!==saved.position[i])) ? project(input.position) : null;
      $('draftPin').hidden = !candidate || candidate[0]<0 || candidate[0]>width || candidate[1]<0 || candidate[1]>height;
      if (!$('draftPin').hidden) {
        $('draftPin').style.left=candidate[0]+'px'; $('draftPin').style.top=candidate[1]+'px';
      }
      const camera = input?.type==='camera' && validVector(input.position) ? input : devices.find(d=>d.id===selected&&d.type==='camera');
      $('rayPath').setAttribute('d',interior()?'':pathFor(project,camera));
    }
    function setMode(value) {
      mode=value;
      $('pick').setAttribute('aria-pressed',value==='position'||value==='grid');
      $('freePick').setAttribute('aria-pressed',value==='volume');
      $('gridNodes').hidden=value!=='grid';
      $('gallery').setAttribute('aria-pressed',value==='gallery');
      $('aim').setAttribute('aria-pressed',value==='target');
      $('canvas').classList.toggle('pick',!!value);
      $('canvas').parentElement.classList.toggle('picking',!!value);
      $('hint').textContent=value==='target'?'Kameranın bakacağı noktaya tıklayın.':
        value==='grid'?'Kot katmanını seçin, ardından turkuaz kesişim noktasına tıklayın.':
        value==='volume'?'Gövdeye tıklayın, ardından derinliği ayarlayın.':
        value==='gallery'?'Vurgulanan galeri yüzeyine tıklayın.':value==='position' ?
        'Baraj ya da yamaç üzerinde bir noktaya tıklayın.' :
        'Sürükle: döndür · Tekerlek: yakınlaş · Sağ sürükle: kaydır';
      stage.invalidate();
    }
    function setGridLevel(index){
      gridIndex=index;
      $('gridLevel').value=index;
      stage.setGridLevel(index);
      const container=$('gridNodes');
      gridNodes=grid.levels[index].nodes.map(position=>{
        const node=document.createElement('button');
        node.type='button'; node.className='gridNode';
        node.title='X '+fmt(position[0])+' · Y '+fmt(position[1])+' · Z '+fmt(position[2]);
        node.setAttribute('aria-label',node.title);
        node.addEventListener('click',()=>{
          write(posKeys,position.map(value=>Number(value.toFixed(3))));
          setMode(null);
          status('Izgara kesişimi seçildi. Konumu kontrol edip kaydedin.');
        });
        return {node,position};
      });
      container.replaceChildren(...gridNodes.map(({node})=>node));
    }
    function clearDepth() {
      depthRange=null;
      $('depthField').hidden=true;
    }
    function showDepth() {
      if (!depthRange) return;
      const [front,back]=depthRange, percent=$('depth').valueAsNumber;
      write(posKeys,front.map((n,i)=>Number((n+(back[i]-n)*percent/100).toFixed(3))));
      $('depthRead').textContent=percent+'%';
      stage.invalidate();
    }
    function refreshPick() {
      const gridPlacement=view==='interior' && $('type').value==='internal';
      $('gallery').hidden=$('freePick').hidden=!gridPlacement;
      $('pick').textContent=gridPlacement?'Kesişim seç':'Sahneden konum seç';
    }
    function setView(value) {
      if (view!==value) { view=value; clearDepth(); stage.setView(view); }
      $('sideView').hidden=!interior();
      $('gridField').hidden=$('gridKey').hidden=view!=='interior';
      root.querySelectorAll('[data-view]').forEach(b => b.setAttribute('aria-pressed',b.dataset.view===view));
      refreshPick();
      setMode(null);
    }
    function refreshType() {
      const camera=$('type').value==='camera';
      $('aimFields').hidden=!camera; $('aim').hidden=!camera;
      [...targetKeys,'fov'].forEach(key => { $(key).required=camera; });
      if (camera && !validVector(numbers(targetKeys))) write(targetKeys,[0,35,12]);
      if (camera && !$('fov').value) $('fov').value=42;
      refreshPick();
    }
    function renderDevices() {
      $('items').replaceChildren();
      $('markers').replaceChildren();
      pins=devices.map(device => {
        const meta=types[device.type], row=document.createElement('button'), pin=document.createElement('button'),
          anchor=document.createElement('span'), pinName=document.createElement('span');
        row.className='device'; pin.className='pin';
        row.type=pin.type='button';
        row.dataset.kind=pin.dataset.kind=device.type;
        row.setAttribute('aria-pressed',device.id===selected); pin.setAttribute('aria-pressed',device.id===selected);
        const icon=document.createElement('span'), body=document.createElement('span'), name=document.createElement('strong'),
          kind=document.createElement('span'), id=document.createElement('span');
        icon.className='icon'; icon.textContent=meta.icon;
        name.textContent=device.name; kind.className='kind'; kind.textContent=meta.short;
        id.className='id'; id.textContent=device.id;
        body.append(name,kind); row.append(icon,body,id);
        row.title=device.name;
        pin.hidden=true;
        pin.textContent=device.id;
        pinName.className='pinName'; pinName.textContent=device.name; pin.append(pinName);
        anchor.className='point'; anchor.dataset.kind=device.type; anchor.hidden=true; anchor.setAttribute('aria-hidden','true');
        pin.setAttribute('aria-label',device.name+' · '+device.id);
        row.addEventListener('click',() => edit(device.id)); pin.addEventListener('click',() => edit(device.id));
        $('items').append(row); $('markers').append(anchor,pin);
        return { device, node:pin, anchor };
      });
      stage.invalidate();
    }
    function edit(id, defaultType='surface') {
      const device=devices.find(d=>d.id===id);
      clearDepth();
      editing=true; selected=device?.id??null;
      $('form').hidden=false; $('remove').hidden=!device;
      $('formTitle').textContent=device?device.id+' düzenle':'Yeni cihaz';
      $('name').value=device?.name??''; $('type').value=device?.type??defaultType;
      write(posKeys,device?.position); write(targetKeys,device?.target); $('fov').value=device?.fovDeg??'';
      refreshType();
      if (device?.type==='internal') {
        setView('interior');
        const nearest=grid.levels.reduce((best,level,i)=>Math.abs(level.y-device.position[1])<Math.abs(grid.levels[best].y-device.position[1])?i:best,0);
        setGridLevel(nearest);
      }
      if (device?.type==='camera') setView('outside');
      setMode(null); renderDevices();
      status(device?device.name:'Adını girin ve cihazın konumunu seçin.');
    }
    function close() {
      editing=false; selected=null; $('form').hidden=true; clearDepth(); setMode(null); renderDevices();
    }
    function pick(hit) {
      if (!hit || (mode==='gallery' && !hit.name.startsWith('Galeri_'))) {
        status(mode==='gallery'?'Görünür galeri yüzeyine tıklayın.':'Uygun bir yüzeye tıklayın veya koordinat girin.');
        return;
      }
      if (hit.range) {
        depthRange=hit.range;
        $('depthField').hidden=false;
        $('depth').value=50;
        showDepth(); setMode(null);
        status('Derinliği ayarlayın. Beyaz halkalı nokta kaydedilecek konumu gösterir.');
        return;
      }
      const target=mode==='target';
      write(target?targetKeys:posKeys,hit.point.map(n=>Number(n.toFixed(3))));
      setMode(null); stage.invalidate();
      status(target?'Bakış hedefi seçildi.':'Konum seçildi.');
    }
    stage=createStage($('canvas'),model,{draw,pick,picking:()=>mode==='grid'?null:mode},grid);
    setGridLevel(gridIndex);
    $('loading').hidden=true; $('layout').hidden=false;
    root.querySelectorAll('[data-view]').forEach(b => listen(b,'click',() => setView(b.dataset.view)));
    listen($('sideView'),'click',() => {
      side=!side;
      $('sideView').setAttribute('aria-pressed',side);
      $('sideView').textContent=side?'Çapraz bak':'Yandan bak';
      stage.setSide(side);
    });
    listen($('gridLevel'),'change',() => setGridLevel(Number($('gridLevel').value)));
    listen($('reset'),'click',stage.reset);
    listen($('new'),'click',() => {
      if(interior())setView('outside');
      edit(null); $('name').focus();
    });
    listen($('addInternal'),'click',() => {
      setView('interior'); edit(null,'internal'); setMode('grid'); $('name').focus({preventScroll:true});
      $('canvas').scrollIntoView({block:'nearest'});
      status('Bir kot katmanı ve kesişim noktası seçin. Sonra sensörü adlandırıp kaydedin.');
    });
    listen($('close'),'click',close);
    listen($('type'),'change',() => {
      clearDepth(); refreshType(); setMode(null);
      if ($('type').value==='internal' && !interior()) setView('interior');
      if ($('type').value==='camera') setView('outside');
      stage.invalidate();
    });
    listen($('form'),'input',stage.invalidate);
    posKeys.forEach(key=>listen($(key),'input',clearDepth));
    listen($('depth'),'input',showDepth);
    listen($('pick'),'click',() => {
      if (mode==='position'||mode==='grid') { setMode(null); return; }
      if ($('type').value==='internal' && !interior()) setView('interior');
      if ($('type').value!=='internal' && view==='interior') setView('outside');
      if ($('type').value==='camera') setView('outside');
      clearDepth();
      setMode(view==='interior'?'grid':'position');
    });
    listen($('freePick'),'click',() => { clearDepth(); setMode(mode==='volume'?null:'volume'); });
    listen($('gallery'),'click',() => {
      clearDepth(); setMode(mode==='gallery'?null:'gallery');
    });
    listen($('aim'),'click',() => {
      const next=mode==='target'?null:'target';
      if (next && interior()) setView('outside');
      setMode(next);
    });
    listen($('form'),'submit',event => {
      event.preventDefault();
      const device=draft();
      try { validate(device,profile); }
      catch (error) { status(error.message); return; }
      if (selected) devices=devices.map(item => item.id===selected?device:item);
      else {
        const prefix=device.type==='camera'?'K-':'D-';
        do { device.id=prefix+crypto.getRandomValues(new Uint32Array(1))[0].toString(36).toUpperCase(); }
        while (devices.some(item=>item.id===device.id));
        devices=[...devices,device];
      }
      edit(device.id); void saveLayout(device.id+' kaydedildi.');
    });
    listen($('remove'),'click',() => {
      const id=selected;
      devices=devices.filter(device=>device.id!==id);
      close(); void saveLayout(id+' silindi.');
    });
    listen($('export'),'click',() => {
      const url=URL.createObjectURL(new Blob([JSON.stringify(layout(),null,2)],{type:'application/json'}));
      const link=document.createElement('a');
      link.href=url; link.download='baraj-cihazlari.json'; link.click();
      setTimeout(()=>URL.revokeObjectURL(url),1000);
    });
    listen($('import'),'click',() => $('file').click());
    listen($('file'),'change',async () => {
      try {
        const file=$('file').files[0];
        if (!file) return;
        const next=readLayout(JSON.parse(await file.text()),model);
        if (events.signal.aborted) return;
        devices=next; close(); await saveLayout(devices.length+' cihaz yüklendi.');
      } catch (error) {
        status(error instanceof SyntaxError?'Geçerli bir JSON dosyası seçin.':error.message);
      } finally { if (!events.signal.aborted) $('file').value=''; }
    });
    renderDevices();
  } catch (error) {
    if (!events.signal.aborted) {
      $('loading').hidden=false; $('layout').hidden=true;
      $('loading').textContent=error.message;
    }
  }
}

export function unmount(host) {
  instances.get(host)?.();
  instances.delete(host);
}
