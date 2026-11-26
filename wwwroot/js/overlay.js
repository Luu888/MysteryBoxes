// null = pierwsze pobranie po załadowaniu strony: już otwarte paczki pokazujemy bez animacji i dźwięku.
let previousOpened=null;let currentSound='/sounds/reveal.mp3';let revealTimer=null;let refreshing=false;let lastState=null;
// Skalowanie płótna 1920x1080 do rozmiaru okna (np. 2560x1440 lub 3840x2160 w OBS).
function fitStage(){const scale=Math.min(innerWidth/1920,innerHeight/1080);document.getElementById('stage').style.transform=`translate(${(innerWidth-1920*scale)/2}px,${(innerHeight-1080*scale)/2}px) scale(${scale})`}
addEventListener('resize',fitStage);fitStage();
if(!window.obsstudio)document.body.classList.add('preview');
function renderBoxes(id,prizes,columns){
 const grid=document.getElementById(id);grid.dataset.columns=columns;grid.innerHTML='';
 grid.style.gridTemplateColumns=`repeat(${columns},minmax(0,1fr))`;grid.style.gridTemplateRows=`repeat(${Math.ceil(prizes.length/columns)},minmax(0,1fr))`;
 for(const p of prizes){const d=document.createElement('div');d.className='box'+(p.isOpened?' opened':'');
  if(p.isOpened){const img=document.createElement('img');img.src=p.imageUrl||'/images/gift.svg';img.alt=p.name;const name=document.createElement('span');name.className='prize-name';name.textContent=p.name;d.append(img,name)}
  else{const n=document.createElement('span');n.className='number';n.textContent=p.number;d.append(n)}
  grid.append(d)}
}
function renderSponsors(images,text){
 document.getElementById('sponsors').hidden=!images.length&&!text;
 document.getElementById('sponsorText').textContent=text;
 const host=document.getElementById('sponsorLogos');host.innerHTML='';
 const n=images.length;host.style.gridTemplateColumns=`repeat(${n<=1?1:n<=4?2:n<=9?3:4},minmax(0,1fr))`;
 for(const url of images){const img=document.createElement('img');img.src=url;img.alt='';host.append(img)}
}
function showReveal(prize){
 const layer=document.getElementById('reveal');const img=document.getElementById('revealImage');
 document.getElementById('revealName').textContent=prize.name||'Niespodzianka!';
 document.getElementById('revealNumber').textContent='PACZKA NR '+prize.number;
 img.src=prize.imageUrl||'/images/gift.svg';layer.classList.remove('show');void layer.offsetWidth;layer.classList.add('show');layer.setAttribute('aria-hidden','false');
 const audio=document.getElementById('revealSound');audio.src=currentSound;audio.currentTime=0;audio.volume=0.9;audio.play().catch(()=>{try{const C=window.AudioContext||window.webkitAudioContext;if(!C)return;const c=new C();[523.25,659.25,783.99,1046.5].forEach((hz,i)=>{const o=c.createOscillator(),g=c.createGain();o.frequency.value=hz;o.type='sine';g.gain.setValueAtTime(.0001,c.currentTime+i*.12);g.gain.exponentialRampToValueAtTime(.12,c.currentTime+i*.12+.03);g.gain.exponentialRampToValueAtTime(.0001,c.currentTime+i*.12+.28);o.connect(g);g.connect(c.destination);o.start(c.currentTime+i*.12);o.stop(c.currentTime+i*.12+.3)});setTimeout(()=>c.close(),1800)}catch{}});
 clearTimeout(revealTimer);revealTimer=setTimeout(()=>{layer.classList.remove('show');layer.setAttribute('aria-hidden','true')},6500);
}
async function refresh(){if(refreshing)return;refreshing=true;try{
 const r=await fetch('/api/state',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const raw=await r.text();
 // Przerysowujemy tylko po zmianie, żeby grafiki (np. GIF-y sponsorów) nie migały.
 if(raw===lastState)return;const s=JSON.parse(raw);
 document.getElementById('title').textContent=s.title;
 const root=document.documentElement.style;root.setProperty('--bg',s.background);root.setProperty('--box',s.boxColor);root.setProperty('--accent',s.accent);
 const perSide=s.columns<=4?2:s.columns>=7?4:3;const half=Math.ceil(s.prizes.length/2);
 renderBoxes('boxesLeft',s.prizes.slice(0,half),perSide);renderBoxes('boxesRight',s.prizes.slice(half),perSide);
 renderSponsors(s.sponsors,s.sponsorText);
 currentSound=s.animationSoundUrl||'/sounds/reveal.mp3';
 const nowOpened=new Set(s.prizes.filter(p=>p.isOpened).map(p=>p.number));
 const newlyOpened=previousOpened&&s.prizes.find(p=>p.isOpened&&!previousOpened.has(p.number));if(newlyOpened)showReveal(newlyOpened);
 previousOpened=nowOpened;lastState=raw;
 }catch(e){console.error(e)}finally{refreshing=false}}
refresh();setInterval(refresh,1200);
