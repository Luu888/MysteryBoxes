let previousOpened=new Set();let currentSound='/sounds/reveal.mp3';let revealTimer=null;
function showReveal(prize){
 const layer=document.getElementById('reveal');const img=document.getElementById('revealImage');
 document.getElementById('revealName').textContent=prize.name||'Niespodzianka!';
 document.getElementById('revealNumber').textContent='PACZKA NR '+prize.number;
 img.src=prize.imageUrl||'/images/gift.svg';layer.classList.remove('show');void layer.offsetWidth;layer.classList.add('show');layer.setAttribute('aria-hidden','false');
 const audio=document.getElementById('revealSound');audio.src=currentSound;audio.currentTime=0;audio.volume=0.9;audio.play().catch(()=>{try{const C=window.AudioContext||window.webkitAudioContext;if(!C)return;const c=new C();[523.25,659.25,783.99,1046.5].forEach((hz,i)=>{const o=c.createOscillator(),g=c.createGain();o.frequency.value=hz;o.type='sine';g.gain.setValueAtTime(.0001,c.currentTime+i*.12);g.gain.exponentialRampToValueAtTime(.12,c.currentTime+i*.12+.03);g.gain.exponentialRampToValueAtTime(.0001,c.currentTime+i*.12+.28);o.connect(g);g.connect(c.destination);o.start(c.currentTime+i*.12);o.stop(c.currentTime+i*.12+.3)});setTimeout(()=>c.close(),1800)}catch{}});
 clearTimeout(revealTimer);revealTimer=setTimeout(()=>{layer.classList.remove('show');layer.setAttribute('aria-hidden','true')},6500);
}
async function refresh(){try{
 const s=await fetch('/api/state',{cache:'no-store'}).then(r=>r.json());
 document.getElementById('title').textContent=s.title;
 document.getElementById('status').textContent=({Open:'ZAPISY OTWARTE',Closed:'ZAPISY ZAMKNIĘTE',WinnerSelected:'WYBIERZ PACZKĘ',Completed:'BOX OPENED',Cancelled:'RUNDA ANULOWANA'})[s.status]||'OCZEKIWANIE';
 document.getElementById('winner').textContent=s.winner?'Zwycięzca: '+s.winner:'';document.getElementById('count').textContent='Uczestnicy: '+s.participants;
 const stage=document.querySelector('.stage');stage.style.background=s.background;const grid=document.getElementById('boxes');grid.style.gridTemplateColumns=`repeat(${s.columns},minmax(0,1fr))`;grid.innerHTML='';
 const nowOpened=new Set();
 for(const p of s.prizes){const d=document.createElement('div');d.className='box'+(p.isOpened?' opened':'');d.style.backgroundColor=s.boxColor;
 if(p.isOpened){nowOpened.add(p.number);const img=document.createElement('img');img.src=p.imageUrl||'/images/gift.svg';img.alt=p.name;const name=document.createElement('span');name.className='prize-name';name.textContent=p.name;d.append(img,name)}
 else{const n=document.createElement('span');n.className='number';n.textContent=p.number;d.append(n)}grid.append(d)}
 const newlyOpened=s.prizes.find(p=>p.isOpened&&!previousOpened.has(p.number));if(newlyOpened)showReveal(newlyOpened);
 previousOpened=nowOpened;currentSound=s.animationSoundUrl||'/sounds/reveal.mp3';
 document.documentElement.style.setProperty('--accent',s.accent);
 }catch(e){console.error(e)}}
refresh();setInterval(refresh,1200);
