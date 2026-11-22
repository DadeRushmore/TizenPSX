// Original TizenPS vector artwork, GPL-2.0. No third-party images or fonts bundled.
const fs = require('fs'), path = require('path');
const {chromium} = require(process.env.TIZENPS_PLAYWRIGHT || 'playwright');
const root = path.resolve(__dirname, '..');
const esc = s => s.replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const emblem = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120"><defs><linearGradient id="a" x2="1" y2="1"><stop stop-color="#b279ff"/><stop offset="1" stop-color="#46e4dc"/></linearGradient></defs><rect x="12" y="12" width="96" height="96" rx="28" fill="#171128" stroke="url(#a)" stroke-width="7"/><path d="M48 34L84 60L48 86Z" fill="url(#a)"/><circle cx="30" cy="30" r="5" fill="#e8daff"/></svg>';
(async () => {
 const browser = await chromium.launch({headless:true,channel:'msedge'});
 try {
  const page = await browser.newPage({viewport:{width:600,height:800},deviceScaleFactor:1});
  async function render(svg, target, w, h) {
   fs.mkdirSync(path.dirname(target),{recursive:true});
   await page.setViewportSize({width:w,height:h});
   await page.setContent('<style>html,body{margin:0;background:transparent}svg{width:100vw;height:100vh;display:block}</style>'+svg);
   await page.screenshot({path:target,omitBackground:true});
  }
  fs.writeFileSync(path.join(root,'preview/assets/tizenps-emblem.svg'),emblem);
  await render(emblem,path.join(root,'roelofs-psx-native/res/ui/tizenps-emblem.png'),400,400);
  await render(emblem,path.join(root,'roelofs-psx-native/shared/res/PSXNative.png'),512,512);
  const wordmark='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 520 152"><text x="0" y="112" font-family="Segoe UI,Arial,sans-serif" font-size="104" font-weight="600" letter-spacing="-5" fill="#ffffff">Tizen<tspan fill="#69dedb">PS</tspan></text></svg>';
  await render(wordmark,path.join(root,'roelofs-psx-native/res/ui/tizenps-wordmark.png'),520,152);
  fs.copyFileSync(path.join(root,'roelofs-psx-native/res/ui/tizenps-wordmark.png'),path.join(root,'preview/assets/tizenps-wordmark.png'));
  const games=JSON.parse(fs.readFileSync(path.join(root,'catalog.json'),'utf8'));
  for(let i=0;i<games.length;i++) {
   const g=games[i],hue=(260+i*41)%360,lines=[];let line='';
   for(const word of g.title.split(' ')){if((line+' '+word).trim().length>19&&line){lines.push(line);line=word;}else line=(line+' '+word).trim();}if(line)lines.push(line);
   const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 800"><defs><linearGradient id="b" x2=".8" y2="1"><stop stop-color="hsl(${hue},55%,25%)"/><stop offset="1" stop-color="#100c1c"/></linearGradient><linearGradient id="c"><stop stop-color="hsl(${hue},90%,75%)"/><stop offset="1" stop-color="#69dedb"/></linearGradient></defs><rect width="600" height="800" fill="url(#b)"/><g fill="none" stroke="url(#c)" stroke-width="20" opacity=".7"><circle cx="440" cy="245" r="210"/><circle cx="440" cy="245" r="145"/><path d="M70 430L500 90M70 510L500 170"/></g><text x="42" y="62" font-family="Segoe UI,Arial" font-size="19" letter-spacing="4" fill="#eee7ff">TIZENPS / LIBRARY ${String(i+1).padStart(2,'0')}</text><rect y="540" width="600" height="260" fill="#100c1c" opacity=".9"/>${lines.map((l,n)=>`<text x="42" y="${602+n*51}" font-family="Segoe UI,Arial" font-size="43" font-weight="600" fill="#fff">${esc(l)}</text>`).join('')}<text x="42" y="758" font-family="Segoe UI,Arial" font-size="17" letter-spacing="2" fill="#aea2c4">ORIGINAL PLACEHOLDER ARTWORK</text></svg>`;
   const dest=path.join(root,'preview/assets/demo-covers',g.id+'.svg');fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,svg);
   await render(svg,path.join(root,'roelofs-psx-native/res/demo-covers',g.id+'.png'),600,800);
  }
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
