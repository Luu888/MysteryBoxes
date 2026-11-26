const $=id=>document.getElementById(id);let adminState=null;
function toast(message,type='success'){
 const host=$('toastContainer');if(!host)return;
 const item=document.createElement('div');item.className='toast toast-'+type;item.textContent=message;
 host.appendChild(item);requestAnimationFrame(()=>item.classList.add('show'));
 setTimeout(()=>{item.classList.remove('show');setTimeout(()=>item.remove(),250)},3500);
}
async function api(url,body){
 const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:body?JSON.stringify(body):'{}'});
 const raw=await r.text(); let data={};
 if(raw){try{data=JSON.parse(raw)}catch{data={message:raw}}}
 if(!r.ok)throw new Error(data.detail||data.message||data.title||`Błąd HTTP ${r.status}: ${r.statusText}`);
 return data;
}
async function readJson(url){
 const r=await fetch(url,{cache:'no-store'});
 const raw=await r.text();let data={};
 if(raw){try{data=JSON.parse(raw)}catch{data={message:raw}}}
 if(!r.ok)throw new Error(data.detail||data.message||data.title||`Błąd HTTP ${r.status}: ${r.statusText}`);
 return data;
}
async function run(url,body){try{const d=await api(url,body);const ok=d.ok!==false;const msg=d.message||'Wykonano.';$('result').textContent=msg;toast(msg,ok?'success':'error')}catch(e){$('result').textContent=e.message;toast(e.message,'error')}await loadBoard()}
async function action(name){await run('/api/admin/'+name);await load()}
async function resetBoxes(){if(confirm('Zakryć wszystkie paczki? Odkryte nagrody znów będą ukryte.'))await action('reset')}
async function openBox(n){if(confirm('Otworzyć paczkę nr '+n+'?'))await run('/api/admin/open',{number:n})}
async function saveSettings(){try{
 await api('/api/admin/title',{title:$('titleInput').value.trim()});
 await api('/api/admin/settings',{background:$('bg').value,box:$('box').value,accent:$('accent').value,columns:+$('columns').value});
 $('result').textContent='Zapisano wygląd widżetu.';toast('Wygląd widżetu został zapisany.');await load();
 }catch(e){$('result').textContent=e.message;toast(e.message,'error')}}
async function upload(file){const f=new FormData();f.append('file',file);const r=await fetch('/api/admin/upload',{method:'POST',body:f});const raw=await r.text();let d={};if(raw){try{d=JSON.parse(raw)}catch{d={message:raw}}}if(!r.ok)throw new Error(d.detail||d.message||d.title||`Błąd HTTP ${r.status}`);if(!d.url)throw new Error('Serwer nie zwrócił adresu przesłanego pliku.');return d.url}
async function savePrize(n){try{const name=$('name'+n).value;let image=$('image'+n).value;const file=$('file'+n).files[0];if(file)image=await upload(file);const d=await api('/api/admin/prizes',{number:n,name,imageUrl:image});if(d.ok===false)throw new Error(d.message);$('result').textContent='Zapisano paczkę '+n;toast('Zapisano paczkę '+n+'.');await load()}catch(e){$('result').textContent=e.message;toast(e.message,'error')}}
function renderPrizes(prizes){const el=$('prizes');el.innerHTML='';for(const p of prizes){const row=document.createElement('div');row.className='prize-row';row.innerHTML=`<img src="${p.imageUrl}" alt=""><input id="name${p.number}" value="${escapeHtml(p.name)}" aria-label="Nazwa"><input id="image${p.number}" value="${escapeHtml(p.imageUrl)}" aria-label="URL grafiki"><input id="file${p.number}" type="file" accept="image/png,image/jpeg,image/webp,image/gif"><button onclick="savePrize(${p.number})">Zapisz ${p.number}</button>`;el.append(row)}}
function escapeHtml(s){return String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
function renderBoard(v){
 const el=$('board');el.style.gridTemplateColumns=`repeat(${v.columns},minmax(0,1fr))`;
 // Przerysowujemy tylko po zmianie, żeby odświeżanie nie gubiło fokusu ani kliknięć.
 const key=JSON.stringify(v.prizes);if(el.dataset.key===key)return;el.dataset.key=key;el.innerHTML='';
 for(const p of v.prizes){const b=document.createElement('button');b.type='button';b.className='tile';
  if(p.isOpened){b.disabled=true;const img=document.createElement('img');img.src=p.imageUrl||'/images/gift.svg';img.alt='';const name=document.createElement('span');name.textContent=p.name;const nr=document.createElement('small');nr.textContent='nr '+p.number+' • otwarta';b.append(img,name,nr)}
  else{b.textContent=p.number;b.onclick=()=>openBox(p.number)}
  el.append(b)}
}
async function loadBoard(){renderBoard(await readJson('/api/state'))}
async function load(){
 const v=await readJson('/api/state');adminState=v;
 $('titleInput').value=v.title;$('bg').value=v.background;$('box').value=v.boxColor;$('accent').value=v.accent;$('columns').value=v.columns;
 $('soundPreview').src=v.animationSoundUrl||'/sounds/reveal.mp3';$('soundStatus').textContent='Aktualny dźwięk: '+(v.animationSoundUrl||'/sounds/reveal.mp3');
 $('overlayUrl').value=location.origin+'/Overlay';
 renderPrizes(v.prizes);renderBoard(v);
}
load().catch(e=>$('result').textContent=e.message);setInterval(()=>loadBoard().catch(()=>{}),3000);
async function uploadAnimationSound(){try{const file=$('soundFile').files[0];if(!file)throw new Error('Wybierz plik dźwiękowy.');const f=new FormData();f.append('file',file);const r=await fetch('/api/admin/upload-sound',{method:'POST',body:f});const raw=await r.text();let d={};if(raw){try{d=JSON.parse(raw)}catch{d={message:raw}}}if(!r.ok)throw new Error(d.detail||d.message||d.title||`Błąd HTTP ${r.status}`);if(!d.url)throw new Error('Serwer nie zwrócił adresu pliku.');await api('/api/admin/animation-sound',{soundUrl:d.url});$('soundPreview').src=d.url;$('soundStatus').textContent='Aktualny dźwięk: '+file.name;$('result').textContent='Zapisano dźwięk animacji.';toast('Zapisano dźwięk animacji.');await load()}catch(e){$('result').textContent=e.message;toast(e.message,'error')}}
async function resetAnimationSound(){try{await api('/api/admin/animation-sound',{soundUrl:'/sounds/reveal.mp3'});$('soundPreview').src='/sounds/reveal.mp3';$('soundStatus').textContent='Aktualny dźwięk: domyślny';$('result').textContent='Przywrócono domyślny dźwięk.';toast('Przywrócono domyślny dźwięk.');await load()}catch(e){$('result').textContent=e.message;toast(e.message,'error')}}
function previewAnimationSound(){const a=$('soundPreview');if(!a.src)a.src=adminState?.animationSoundUrl||'/sounds/reveal.mp3';a.play().catch(()=>{$('result').textContent='Nie udało się odtworzyć dźwięku.'})}

function openOverlay(){window.open(location.origin+'/Overlay','_blank','noopener,noreferrer')}
async function copyOverlayUrl(){const url=location.origin+'/Overlay';try{await navigator.clipboard.writeText(url);$('result').textContent='Skopiowano link do OBS.'}catch{$('overlayUrl').select();document.execCommand('copy');$('result').textContent='Skopiowano link do OBS.'}toast('Skopiowano link do OBS.')}
