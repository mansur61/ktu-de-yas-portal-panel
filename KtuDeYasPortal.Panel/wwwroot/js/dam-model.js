const instances=new WeakMap();
export function init(root){
 destroy(root);const view=root.querySelector('.model-perspective'),world=root.querySelector('.model-world');
 let yaw=-18,pitch=28,zoom=1,drag=null;const abort=new AbortController(),options={signal:abort.signal};
 const render=()=>{const fit=Math.min(view.clientWidth/680,view.clientHeight/500);world.style.transform=`scale(${Math.max(.15,fit)*zoom}) rotateX(${pitch}deg) rotateY(${yaw}deg)`;world.style.setProperty('--yaw',`${-yaw}deg`);world.style.setProperty('--pitch',`${-pitch}deg`);root.dataset.yaw=String(yaw);root.dataset.pitch=String(pitch);};
 view.addEventListener('pointerdown',e=>{if(e.target.closest('button,a,input'))return;drag={x:e.clientX,y:e.clientY,id:e.pointerId};view.setPointerCapture(e.pointerId);view.classList.add('dragging');},options);
 view.addEventListener('pointermove',e=>{if(!drag||drag.id!==e.pointerId)return;yaw+=(e.clientX-drag.x)*.55;pitch-=(e.clientY-drag.y)*.55;drag.x=e.clientX;drag.y=e.clientY;render();},options);
 const stop=()=>{drag=null;view.classList.remove('dragging');};view.addEventListener('pointerup',stop,options);view.addEventListener('pointercancel',stop,options);
 view.addEventListener('wheel',e=>{e.preventDefault();zoom=Math.max(.5,Math.min(2.5,zoom-e.deltaY*.001));render();},{...options,passive:false});
 view.addEventListener('keydown',e=>{if(e.target!==view)return;const steps={ArrowLeft:[-15,0],ArrowRight:[15,0],ArrowUp:[0,-15],ArrowDown:[0,15]};if(steps[e.key]){e.preventDefault();yaw+=steps[e.key][0];pitch+=steps[e.key][1];render();}},options);
 root.querySelector('[data-model-reset]')?.addEventListener('click',()=>{yaw=-18;pitch=28;zoom=1;render();},options);
 const observer=new ResizeObserver(render);observer.observe(view);instances.set(root,{abort,observer});render();
}
export function setImage(root,visible){root.classList.toggle('hide-reference',!visible);}
export function destroy(root){const state=instances.get(root);if(state){state.abort.abort();state.observer.disconnect();instances.delete(root);}}
