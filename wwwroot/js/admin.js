const $=id=>document.getElementById(id);let adminState=null;
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
async function action(name){try{const d=await api('/api/admin/'+name);$('result').textContent=d.message||d.winner||'Wykonano.';await load()}catch(e){$('result').textContent=e.message}}
async function saveTitle(){try{await api('/api/admin/title',{title:$('titleInput').value});$('result').textContent='Zapisano tytuł.';load()}catch(e){$('result').textContent=e.message}}
async function saveSettings(){try{await api('/api/admin/settings',{background:$('bg').value,box:$('box').value,accent:$('accent').value,columns:+$('columns').value});$('result').textContent='Zapisano wygląd.';load()}catch(e){$('result').textContent=e.message}}
async function saveSubLuck(){try{const d=await api('/api/admin/subluck',{multiplier:+$('subLuck').value});$('result').textContent=d.message||'Zapisano SubLuck.';load()}catch(e){$('result').textContent=e.message}}
async function upload(file){const f=new FormData();f.append('file',file);const r=await fetch('/api/admin/upload',{method:'POST',body:f});const raw=await r.text();let d={};if(raw){try{d=JSON.parse(raw)}catch{d={message:raw}}}if(!r.ok)throw new Error(d.detail||d.message||d.title||`Błąd HTTP ${r.status}`);if(!d.url)throw new Error('Serwer nie zwrócił adresu przesłanego pliku.');return d.url}
async function savePrize(n){const name=$('name'+n).value;let image=$('image'+n).value;const file=$('file'+n).files[0];if(file)image=await upload(file);await api('/api/admin/prizes',{number:n,name,imageUrl:image});$('result').textContent='Zapisano paczkę '+n}
function renderPrizes(prizes){const el=$('prizes');el.innerHTML='';for(const p of prizes){const row=document.createElement('div');row.className='prize-row';row.innerHTML=`<img src="${p.imageUrl}" alt=""><input id="name${p.number}" value="${escapeHtml(p.name)}" aria-label="Nazwa"><input id="image${p.number}" value="${escapeHtml(p.imageUrl)}" aria-label="URL grafiki"><input id="file${p.number}" type="file" accept="image/png,image/jpeg,image/webp,image/gif"><button onclick="savePrize(${p.number})">Zapisz ${p.number}</button>`;el.append(row)}}
function escapeHtml(s){return String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
async function load(){
 const s=await readJson('/api/admin/state');adminState=s;const v=s.state;
 $('titleInput').value=v.title;$('bg').value=v.background;$('box').value=v.boxColor;$('accent').value=v.accent;$('columns').value=v.columns;$('subLuck').value=v.subLuckMultiplier||1;
 $('soundPreview').src=v.animationSoundUrl||'/sounds/reveal.mp3';$('soundStatus').textContent='Aktualny dźwięk: '+(v.animationSoundUrl||'/sounds/reveal.mp3');
 $('overlayUrl').value=location.origin+'/Overlay';
 $('roundState').textContent=`Status: ${v.status} • Uczestnicy: ${v.participants} • Zwycięzca: ${v.winner||'—'}`;
 renderPrizes(v.prizes);$('history').innerHTML=s.rounds.map(r=>`<p>#${r.id} • ${r.status} • ${r.winnerName||'—'} • paczka ${r.chosenPrizeNumber||'—'}</p>`).join('')||'Brak historii.';
 await refreshTwitchStatus();
}
async function refreshTwitchStatus(){
 const el=$('twitchStatus');const connect=$('connectTwitchButton');const disconnect=$('disconnectTwitchButton');
 try{
   const t=await readJson('/api/admin/twitch/status');
   if(t.connected){
     el.className='status-connected';
     el.innerHTML=`<span class="status-dot"></span><strong>Połączono z Twitchem</strong><br>Konto: ${escapeHtml(t.name||'Nieznane')}<br><small>Token wygasa: ${t.expiresAt?new Date(t.expiresAt).toLocaleString():'brak danych'}</small>`;
     connect.textContent='Połącz ponownie';disconnect.disabled=false;
   }else{
     el.className='status-disconnected';
     el.innerHTML='<span class="status-dot"></span><strong>Nie połączono z Twitchem</strong><br><small>Połącz konto, aby uruchomić odbieranie wiadomości z czatu.</small>';
     connect.textContent='Połącz konto Twitch';disconnect.disabled=true;
   }
 }catch(e){
   el.className='status-error';el.textContent='Nie udało się sprawdzić połączenia Twitch: '+e.message;
   disconnect.disabled=true;
 }
}

async function disconnectTwitch(){try{await api('/api/admin/twitch/disconnect');await refreshTwitchStatus();$('result').textContent='Rozłączono konto Twitch.'}catch(e){$('result').textContent=e.message}}
load().catch(e=>$('result').textContent=e.message);setInterval(()=>refreshTwitchStatus().catch(()=>{}),10000);
async function uploadAnimationSound(){try{const file=$('soundFile').files[0];if(!file)throw new Error('Wybierz plik dźwiękowy.');const f=new FormData();f.append('file',file);const r=await fetch('/api/admin/upload-sound',{method:'POST',body:f});const raw=await r.text();let d={};if(raw){try{d=JSON.parse(raw)}catch{d={message:raw}}}if(!r.ok)throw new Error(d.detail||d.message||d.title||`Błąd HTTP ${r.status}`);if(!d.url)throw new Error('Serwer nie zwrócił adresu pliku.');await api('/api/admin/animation-sound',{soundUrl:d.url});$('soundPreview').src=d.url;$('soundStatus').textContent='Aktualny dźwięk: '+file.name;$('result').textContent='Zapisano dźwięk animacji.';await load()}catch(e){$('result').textContent=e.message}}
async function resetAnimationSound(){try{await api('/api/admin/animation-sound',{soundUrl:'/sounds/reveal.mp3'});$('soundPreview').src='/sounds/reveal.mp3';$('soundStatus').textContent='Aktualny dźwięk: domyślny';$('result').textContent='Przywrócono domyślny dźwięk.';load()}catch(e){$('result').textContent=e.message}}
function previewAnimationSound(){const a=$('soundPreview');if(!a.src)a.src=adminState?.state?.animationSoundUrl||'/sounds/reveal.mp3';a.play().catch(()=>{$('result').textContent='Nie udało się odtworzyć dźwięku.'})}

function openOverlay(){window.open(location.origin+'/Overlay','_blank','noopener,noreferrer')}
async function copyOverlayUrl(){const url=location.origin+'/Overlay';try{await navigator.clipboard.writeText(url);$('result').textContent='Skopiowano link do OBS.'}catch{$('overlayUrl').select();document.execCommand('copy');$('result').textContent='Skopiowano link do OBS.'}}
