const instances = new WeakMap();

// Görünüm açıları [yaw°, pitch°, zoom]
const VIEWS = {
  mansap: { yaw: -18,  pitch: 28, zoom: 1    },
  memba:  { yaw:  162, pitch: 22, zoom: 1    },
  yan:    { yaw:   72, pitch: 24, zoom: 1    },
  capraz: { yaw:   42, pitch: 46, zoom: 0.85 },
  ust:    { yaw:  -18, pitch: 70, zoom: 0.9  },
};

export function init(root) {
  destroy(root);
  const view  = root.querySelector('.model-perspective');
  const world = root.querySelector('.model-world');
  if (!view || !world) return;

  let yaw = VIEWS.mansap.yaw, pitch = VIEWS.mansap.pitch, zoom = 1, drag = null;
  const abort   = new AbortController();
  const options = { signal: abort.signal };

  const render = () => {
    const fit = Math.min(view.clientWidth / 680, view.clientHeight / 500);
    world.style.transform = `scale(${Math.max(.15, fit) * zoom}) rotateX(${pitch}deg) rotateY(${yaw}deg)`;
    world.style.setProperty('--yaw',   `${-yaw}deg`);
    world.style.setProperty('--pitch', `${-pitch}deg`);
    root.dataset.yaw   = String(yaw);
    root.dataset.pitch = String(pitch);
  };

  view.addEventListener('pointerdown', e => {
    if (e.target.closest('button,a,input')) return;
    drag = { x: e.clientX, y: e.clientY, id: e.pointerId };
    view.setPointerCapture(e.pointerId);
    view.classList.add('dragging');
  }, options);

  view.addEventListener('pointermove', e => {
    if (!drag || drag.id !== e.pointerId) return;
    yaw   += (e.clientX - drag.x) * .55;
    pitch -= (e.clientY - drag.y) * .55;
    drag.x = e.clientX; drag.y = e.clientY;
    render();
  }, options);

  const stop = () => { drag = null; view.classList.remove('dragging'); };
  view.addEventListener('pointerup',     stop, options);
  view.addEventListener('pointercancel', stop, options);

  view.addEventListener('wheel', e => {
    e.preventDefault();
    zoom = Math.max(.5, Math.min(2.5, zoom - e.deltaY * .001));
    render();
  }, { ...options, passive: false });

  view.addEventListener('keydown', e => {
    if (e.target !== view) return;
    const steps = { ArrowLeft: [-15, 0], ArrowRight: [15, 0], ArrowUp: [0, -15], ArrowDown: [0, 15] };
    if (steps[e.key]) { e.preventDefault(); yaw += steps[e.key][0]; pitch += steps[e.key][1]; render(); }
  }, options);

  // Sığdır butonu
  root.querySelector('[data-model-reset]')?.addEventListener('click', () => {
    ({ yaw, pitch, zoom } = VIEWS.mansap); render();
  }, options);

  const observer = new ResizeObserver(render);
  observer.observe(view);
  instances.set(root, { abort, observer, getState: () => ({ yaw, pitch, zoom }),
    applyView: v => { yaw = v.yaw; pitch = v.pitch; zoom = v.zoom; render(); } });
  render();
}

export function setView(root, viewName) {
  const state = instances.get(root);
  const v = VIEWS[viewName] ?? VIEWS.mansap;
  if (state) state.applyView(v);
}

export function reset(root) {
  const state = instances.get(root);
  if (state) state.applyView(VIEWS.mansap);
}

export function setImage(root, visible) {
  root.classList.toggle('hide-reference', !visible);
}

export function destroy(root) {
  const state = instances.get(root);
  if (state) { state.abort.abort(); state.observer.disconnect(); instances.delete(root); }
}
