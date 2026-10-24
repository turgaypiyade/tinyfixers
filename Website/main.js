(() => {
  const TASKS = {
    tr: {
      1: ["Zemini Sağlamlaştır", "Sütunları Kaynakla", "Çan Katını Onar", "Bayrağı Dik"],
      2: ["Ayakları Güçlendir", "Askı Tellerini Bağla", "Turuncu Boyayı Sür", "Işıkları Yak"],
      3: ["Dört Ayağı Sabitle", "İkinci Katı Yükselt", "Boyayı Yenile", "Işık Şovunu Başlat"],
      4: ["Kumu Temizle", "Taş Blokları Taşı", "Kireç Taşını Parlat", "Altın Tepeyi Yerleştir"],
      5: ["Zemini Hazırla", "Duvarları Yükselt", "Gözetleme Kulelerini Onar", "Pati Bayraklarını Dik"],
      6: ["Patikayı Temizle", "Seyir Alanını Düzenle", "Korkulukları Onar", "Manzara Noktasını Tamamla"],
      7: ["İskeleyi Kur", "Pencereleri Yenile", "Ana Kubbeyi Kapla", "Minareyi Restore Et"],
      8: ["İskeleyi Kur", "Kaideleri Sağlamlaştır", "Güneş Tacını Tak", "Meşaleyi Yak"],
    },
    en: {
      1: ["Reinforce the Ground", "Weld the Columns", "Fix the Bell Chamber", "Raise the Flag"],
      2: ["Strengthen the Piers", "Attach the Suspenders", "Apply the Orange Paint", "Turn On the Lights"],
      3: ["Anchor the Four Legs", "Raise the Second Floor", "Refresh the Paint", "Start the Light Show"],
      4: ["Clear the Sand", "Haul the Stone Blocks", "Polish the Limestone", "Place the Golden Capstone"],
      5: ["Prepare the Ground", "Raise the Walls", "Repair the Watchtowers", "Raise the Paw Flags"],
      6: ["Clear the Trail", "Restore the Lookout", "Repair the Railings", "Complete the Scenic Lookout"],
      7: ["Set Up the Scaffolding", "Restore the Windows", "Reseal the Main Dome", "Restore the Minaret"],
      8: ["Set Up the Scaffold", "Reinforce the Pedestals", "Fit the Sun Crown", "Light the Torch"],
    },
  };
  const WONDERS = {
    tr: { 1: "Eğik Pati Kulesi", 2: "Altın Kuyruk Köprüsü", 3: "Eyfil Kulesi", 4: "Miyav Piramidi", 5: "Büyük Pati Seddi", 6: "Kuyruk Kanyonu", 7: "Ayasofya", 8: "Rodos Devi" },
    en: { 1: "Leaning Paw Tower", 2: "Golden Tail Bridge", 3: "Eiffur Tower", 4: "Purramid", 5: "Great Paw Wall", 6: "Tail Canyon", 7: "Hagia Sophia", 8: "Colossus of Rhodes" },
  };
  const MORE = { tr: "+4 görev daha", en: "+4 more tasks" };

  const TR = {
    launch_cta: "Çıkışta haber ver", launch_eyebrow: "Ekibe katıl", launch_title: "Sıradaki maceran hazırlanıyor.",
    launch_text: "E-postanı bırak, Wonder Fixers çıktığında haber verelim. Yalnızca bir çıkış duyurusu; haber bülteni yok.",
    launch_email: "E-posta adresin", launch_submit: "Beni de ekle",
    launch_consent: "Bir çıkış e-postası almayı kabul ediyorum. hello@wondigames.com adresine yazarak vazgeçebilirim.",
    launch_nojs: "Katılmak için JavaScript’i açabilir veya hello@wondigames.com adresine yazabilirsin.",
    nav_watch: "Oynanışı izle", view_screenshot: "Ekran görüntüsünü büyüt", close_screenshot: "Ekran görüntüsünü kapat",
    skip_content: "İçeriğe geç", nav_label: "Bölümler", menu_label: "Menü", gallery_label: "Oyun galerisi",
    watch_gameplay: "Oynanışı izle", explore_world: "Harikaları keşfet",
    hero_board_alt: "Çiçekler ve antik mühürle renkli bir Wonder Fixers bulmaca tahtası",
    hero_note: "Tamir edilecek koca bir dünya!", trail_puzzle: "Her eşleşmenin bir amacı var",
    trail_wonders: "Ünlü harikaları onar", trail_crew: "Ekibini bul", repair_tasks: "Tamir görevlerini gör",
    gallery_note: "Gerçek oynanış ve ekran görüntüleri. Keşfetmek için kaydır.",
    closing_title: "Sıradaki macera şekilleniyor.", closing_text: "Yeni harikalar, oynanış ve sahne arkası için ekibi takip et.", follow_crew: "Ekibi takip et",
    nav_play: "Oyun", nav_wonders: "Harikalar", nav_crew: "Ekip", nav_studio: "Stüdyo",
    hero_eyebrow: '<span lang="en">Wonder Fixers</span> · Eşleştirme bulmacası',
    hero_title: "Küçük eşleşmeler.<br>Büyük <em>harikalar.</em>",
    hero_lede: "Küçük bir bulmaca, büyük bir macera. Sevimli tamirci ekibine katıl, dünyanın harikalarına yeniden hayat ver.",
    stores_soon: "Yakında", hero_caption: "Ekip iskelede, tamirin ortasında.", stamp: "Tamirde",
    hero_alt: "Ayı, domuz ve koç merdivene tırmanıp eski bir taş duvarı onarıyor",
    team_alt: "Wonder Fixers ekibi bir arada: ayı Brumo, koç Bram, tavşan Pemi, domuz Tinko, maymun Fiko ve kuş Pilo; yanlarında rakipleri porsuk Rumbo ve sırtlan Zarko",
    play_eyebrow: "Nasıl oynanır", play_title: "Geçtiğin her seviye gerçek bir şeyi onarır",
    play_lede: "Özünde klasik bir eşleştirme oyunu; ama devam etmek için bir sebebin var: her galibiyet ünlü bir yapıyı eski günlerine bir adım daha yaklaştırır.",
    s1_title: "Eşleştir", s1_text: "Taşların yerini değiştir, üç ve daha fazlasını yan yana getir. Büyük eşleşmeler roket, PatchBot ve Pulse Core yapar.",
    s2_title: "Yıldız kazan", s2_text: "Seviyenin hedeflerini temizle: sandıklar, çamur, jel, brandalı heykeller. Kazandığın her el yapımı seviye yıldız getirir.",
    s3_title: "Harikayı onar", s3_text: "Yıldızlarını tamir görevlerine harca: sütunları kaynakla, mermeri parlat, bayrağı dik. Hepsi bitince harika yeniden parlar.",
    sh_eyebrow: "Oyundan kareler", sh_title: "Wonder Fixers'ın içinden",
    sh_levels: "Seviyeler", sh_menus: "Harikalar ve etkinlikler",
    sh_play: "Oynanış",
    c1: "Seviye 11", c1t: "Tarzınla temizle", c1_alt: "Seviye 11'in oynanışı: roketler turuncu dolaplarla dolu tahtayı temizliyor",
    c2: "Seviye 67", c2t: "Roket şenliği", c2_alt: "Seviye 67'nin oynanışı: bir roket kombosu bütün tahtayı süpürüyor",
    c3: "Seviye 151", c3t: "Bir hamle daha… ya da değil?", c3_alt: "Seviye 151'in oynanışı: Boğaz manzarası önünde çiçek desenli tahta",
    k_eyebrow: "Alet çantası", k_title: "Taşlar, special'lar ve jokerler",
    k_tiles: "Dört renk, tek kural: aynı taştan üç ya da daha fazlasını yan yana getir. Daha büyük şekiller special verir.",
    k_specials: "Special'lar", k_jokers: "Jokerler", k_combo: "İki special'ı birbirine kaydır, daha büyük bir kombo çıkar.",
    sp1_how: "4'lü sıra", sp1_name: "Rocket", sp1_text: "Bir satırı ya da sütunu baştan sona temizler.",
    sp2_how: "2×2 kare", sp2_name: "Patchbot", sp2_text: "Uçup tahtadaki bir hedefi vurur.",
    sp3_how: "L veya T şeklinde 5'li", sp3_name: "PulseCore", sp3_text: "Etrafındaki 5×5 alanı patlatır.",
    sp4_how: "5'li sıra", sp4_name: "Spark Orb", sp4_text: "Bir rengin tahtadaki tüm taşlarını temizler.",
    bo1_how: "Tek hedef", bo1_name: "Wonder Çekici", bo1_text: "Dokunduğun taşı ya da engeli kırar.",
    bo2_how: "Satır", bo2_name: "Harika Yontucu", bo2_text: "Dokunduğun satırı temizler.",
    bo3_how: "Sütun", bo3_name: "Wonder Asansör", bo3_text: "Dokunduğun sütunu temizler.",
    bo4_how: "Tüm tahta", bo4_name: "Wonder Karıştırıcı", bo4_text: "Tahtadaki bütün taşları karıştırır.",
    bo5_how: "Tek renk, 3 hamle", bo5_name: "Wonder Boya Kovası", bo5_text: "Bir taşa dokun: o renk 3 hamle boyunca tahtadan çıkar.",
    l1: "Çiçek bahçesinde mühür", l1t: "32 hamle", l1_alt: "Antik mühür, çiçekli çim ve sandıklarla elmas şeklinde bir tahta",
    l7: "Fener sıraları", l7t: "25 hamle", l7_alt: "Yüksek pencereli salonda mavi dolapların üstünde turuncu fener sıraları",
    l8: "Sandık duvarı", l8t: "23 hamle", l8_alt: "Tuğla mahzende turuncu sandık ve fenerlerden bir duvar",
    l9: "Çimenlik ve dolaplar", l9t: "30 hamle", l9_alt: "Mavi taş salonda mavi dolapların yanında çimenlik ve çamur",
    l3: "Mıknatıs hattı", l3t: "26 hamle", l3_alt: "Yeşil dolapların üstünde tahtayı dolanan mavi mıknatıs rayları",
    l4: "İkiz kasalar", l4t: "23 hamle", l4_alt: "Renkli tahtanın altında iki üç renkli kasa",
    u1: "Sıradaki harika", u1t: "Ana ekran", u1_alt: "Domuz ve büyük taş surla ana ekran",
    u2: "Altın Kuyruk Köprüsü", u2t: "Tamir sürüyor", u2_alt: "Kırmızı asma köprünün önünde ayıyla ana ekran",
    u3: "Bostan Hasadı", u3t: "Hasat 5/5", u3_alt: "Sebze bostanı ve pazar tezgâhının yanında ayı",
    u4: "Dişli Kupası", u4t: "Takımlar yarışır", u4_alt: "Kürsüde tavşan ve domuzla takım sıralaması",
    u5: "Günlük Çark", u5t: "Her gün bir ücretsiz çevirme", u5_alt: "Mor sahnede ödül çarkı",
    u6: "Bram ve Rumbo", u6t: "Gerçek bir düello, maçın ortasında", u6_alt: "Dolu bir tahtanın üstünde karşı karşıya koç Bram ve porsuk Rumbo",
    ev7: "Dişli Kupası", ev7t: "Takımınla dişli topla. İlk üç takım ödül kazanır.",
    ev8: "Günlük Çark", ev8t: "Her gün ödül çarkını bir kez ücretsiz çevir.",
    lv1: "Brandalı heykeller", lv1t: "Renkleri topla, sonra taşı yont", lv1_alt: "Taş duvarlarla bölünmüş tahtada dört brandalı heykel",
    lv2: "Üç renkli kasa", lv2t: "Her renkten beş taş açar", lv2_alt: "Kırmızı mantar sıralarının yanında dev üç renkli kasa",
    lv3: "Kupa tahtası", lv3t: "Sandıklar ve fenerler, 32 hamle", lv3_alt: "Tahta sandık ve fenerlerden kupa şeklinde bir tahta",
    lv4: "Tüp hattı", lv4t: "Cam tüpleri temizle", lv4_alt: "İki taş sütunu arasında tahtayı boydan geçen cam tüpler",
    lv5: "Mühür ve toplar", lv5t: "Toplar special'larını kafesler", lv5_alt: "Üç lazer topunun üstünde antik bir mühür",
    sh1: "Ana ekran", sh1t: "Harikan ve ekibin", sh1_alt: "Tamirdeki bir saray, önde ayı ve domuz, altta seviye butonu",
    sh2: "Tamir görevleri", sh2t: "Yıldız harca, harikayı onar", sh2_alt: "Her görevi üç yıldız olan tamir listesi",
    sh3: "Bostan Hasadı", sh3t: "Gizli sebzeleri kaz", sh3_alt: "Sebze bostanının yanında ayının olduğu Bostan Hasadı etkinliği",
    w_eyebrow: "Tamir listesinde", w_title: "Ünlü yerler, biraz daha tüylü",
    w_lede: "Her harikanın sekiz tamir görevi var. İşte her iş emrinden birkaçı.",
    chip_first: "İlk iş", chip_next: "Sırada",
    rhodes_where: "Rodos, Yunanistan", rhodes_alt: "Limanın üzerinde yanan meşalesiyle Rodos Devi; kayıktan el sallayan ayı Brumo",
    next_wonder_title: "Sıradaki harika", next_wonder_text: "Yapım aşamasında. Ekibin sıradaki durağı hazırlanıyor.",
    pisa_where: "Pisa, İtalya", gw_where: "Çin Seddi, Çin", canyon2_where: "Büyük Kanyon, ABD", aya_where: "İstanbul, Türkiye",
    gw_alt: "Büyük bir taş surun önünde haritalı domuz Tinko", canyon2_alt: "Alet kutusuyla kanyon seyir noktasında tavşan Pemi", aya_alt: "Ayasofya'nın önünde plan tutan tavşan Pemi, minarelerden birinde iskele", gg_where: "San Francisco, ABD", eiffel_where: "Paris, Fransa", pyr_where: "Gize, Mısır",
   
    pisa_alt: "Eğik bir kulenin önünde plan tutan tavşan", gg_alt: "Kırmızı asma köprüye dürbünle bakan ayı",
    eiffel_alt: "Demir kulenin altında planlarla koç", pyr_alt: "Piramidin önünde çekiçli domuz",
    c_eyebrow: "Tamircilerle tanış", c_title: "Her işe bir aleti olan ekip", c_lede: "Gözlükler takılı, bandanalar bağlı. Birlikte oynayacağın hayvanlar bunlar.",
    ram_role: "Ekip şefi · Koç", ram_name: "Bram", ram_text: "Planları okur, düelloları kazanır, hiçbir işi yarım bırakmaz.", ram_alt: "Haritası ve sırt çantasıyla koç Bram",
    bear_role: "Ağır işler · Ayı", bear_name: "Brumo", bear_text: "Taş blokları o taşır. Bostanın en iyi havuçları da onun.", bear_alt: "Yeşil tulumlu ayı Brumo",
    pig_role: "Usta tamirci · Domuz", pig_name: "Tinko", pig_text: "Cıvatalı, perçinli ya da sıkışmışsa çekiç çıkar.", pig_alt: "İngiliz anahtarı tutan domuz Tinko",
    rabbit_role: "Kâşif · Tavşan", rabbit_name: "Pemi", rabbit_text: "Sahaya ilk o gelir. Çatlakları herkesten önce bulur.", rabbit_alt: "Gözlüklü, elinde planlarla tavşan Pemi",
    monkey_role: "Kaynakçı · Maymun", monkey_name: "Fiko", monkey_text: "Çatlak ekranları da demir kirişleri de aynı sağlam elle onarır.", monkey_alt: "Kaynak maskeli maymun Fiko",
    r_eyebrow: "Boss Düellosu", r_title: "Herkes harikaların onarılmasını istemiyor",
    r_lede: "Özel seviyelerde bir rakip çıkar ve tahta düelloya döner. Eşleşmelerin senin saldırındır.",
    bird_role: "Pilot · Kuş", bird_name: "Pilo", bird_text: "Rotayı gökyüzünden keşfeder, doğru parçayı tam gereken yere bırakır.", bird_alt: "Pilot gözlüklü turuncu kuş Pilo",
    badger_name: "Porsuk Rumbo", badger_text: "Yaptığın her şeyin altını kazar, tahtana çamur fırlatır.", badger_alt: "Kazma tutan porsuk Rumbo",
    hyena_name: "Sırtlan Zarko", hyena_text: "Rumbo'dan sonra gelir, ne varsa söker ve daha da sert vurur.", hyena_alt: "Levyeli sırtlan Zarko",
    rule1: "Taşları eşleştir, saldırını güçlendir", rule2: "Hamlen bitince vurursun, rakip karşılık verir", rule3: "Kalkanlar darbeleri karşılar",
    e_eyebrow: "Hep bir şey var", e_title: "Tamirlerin arasında etkinlikler",
    ev1: "Köprü Tamiri", ev1t: "Köprüdeki kendi bölümünü diğer dört oyuncudan önce bitirmek için yarış.",
    ev2: "Bostan Hasadı", ev2t: "Seviye kazanıp kürek topla, sonra bostanı kaz; gizli sebzeleri ve ödülleri bul.",
    ev3: "Tırmanış", ev3t: "İlk denemede kazanarak yedi durağı tırman. Bir kez kayarsan baştan başlarsın. Bitiren herkes altın havuzunu paylaşır.",
    ev4: "Haftalık Görevler", ev4t: "Birkaç günde bir yeni hedefler, sonunda bir sandık.",
    ev5: "Takımlar", ev5t: "Bir takıma katıl, sohbet et, canın bitince birbirinize can gönderin.",
    ev6: "Boss Düellosu", ev6t: "Özel seviyelerde rakiplerle karşılaş; her biri bir öncekinden zorlu.",
    st_eyebrow: '<span lang="en">Wondi Games</span> hakkında', st_title: "Sıcak ve adil bulmaca oyunları yapan küçük bir stüdyo",
    v1: "Her seviye elle tasarlanır", v1t: "Her tahtayı kendi silüeti ve kendi numarasıyla tek tek kurar, doğru hissettirene dek oynarız.",
    v2: "Hak edilen zorluk", v2t: "Bazı seviyeler birkaç deneme ister. Kazandığında bunun senin başarın olduğunu hissetmelisin.",
    v3: "Dönmeye değer bir dünya", v3t: "Karakterler, yeniden inşa edilecek yerler ve birlikte oynanacak arkadaşlar.",
    cheer_alt: "Elinde havuçla sevinen ayı",
    f_title: "Merhaba de", f_rights: "Wonder Fixers ve karakterleri Wondi Games'e aittir.", f_privacy: "Gizlilik Politikası", f_terms: "Kullanım Şartları",
  };

  const en = {};
  document.querySelectorAll("[data-i18n]").forEach(el => en[el.dataset.i18n] = el.textContent);
  document.querySelectorAll("[data-i18n-html]").forEach(el => en[el.dataset.i18nHtml] = el.innerHTML);
  document.querySelectorAll("[data-i18n-alt]").forEach(el => en[el.dataset.i18nAlt] = el.alt);
  document.querySelectorAll("[data-i18n-aria]").forEach(el => en[el.dataset.i18nAria] = el.getAttribute("aria-label"));
  const dict = { en, tr: TR };

  function apply(lang) {
    lang = Object.prototype.hasOwnProperty.call(dict, lang) ? lang : "en";
    const d = dict[lang] || en;
    document.documentElement.lang = lang;
    document.querySelectorAll("[data-i18n]").forEach(el => { const v = d[el.dataset.i18n]; if (v) el.textContent = v; });
    document.querySelectorAll("[data-i18n-html]").forEach(el => { const v = d[el.dataset.i18nHtml]; if (v) el.innerHTML = v; });
    document.querySelectorAll("[data-i18n-alt]").forEach(el => { const v = d[el.dataset.i18nAlt]; if (v) el.alt = v; });
    document.querySelectorAll("[data-i18n-aria]").forEach(el => { const v = d[el.dataset.i18nAria]; if (v) el.setAttribute("aria-label", v); });
    document.querySelectorAll("[data-w]").forEach(el => el.textContent = WONDERS[lang][el.dataset.w]);
    document.querySelectorAll("[data-tasks]").forEach(ul => {
      const list = TASKS[lang][ul.dataset.tasks];
      ul.replaceChildren(...list.map((t, i) => {
        const li = document.createElement("li");
        li.textContent = t;
        if (ul.dataset.tasks === "1" && i < 2) li.className = "done";
        return li;
      }), Object.assign(document.createElement("li"), { className: "more", textContent: MORE[lang] }));
    });
    document.querySelectorAll(".lang button").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.lang === lang)));
    try { localStorage.setItem("wondi_lang", lang); } catch (e) {}
  }

  // /tr/ sayfası <html data-lang="tr"> taşır → arama motoru da Türkçeyi görür. Kökte tarayıcı diline bakılır.
  const forced = document.documentElement.dataset.lang;
  let lang = forced || "";
  if (!lang) { try { lang = localStorage.getItem("wondi_lang") || ""; } catch (e) { lang = ""; } }
  if (!lang) lang = (navigator.language || "en").toLowerCase().startsWith("tr") ? "tr" : "en";
  apply(lang);
  const onSite = /(^|\.)wondigames\.com$/.test(location.hostname);
  document.querySelectorAll(".lang button").forEach(b => b.addEventListener("click", () => {
    const target = b.dataset.lang;
    if (onSite) {
      try { localStorage.setItem("wondi_lang", target); } catch (e) {}
      const path = target === "tr" ? "/tr/" : "/";
      if (location.pathname !== path) { location.href = path + location.hash; return; }
    }
    apply(target);
  }));
  document.getElementById("year").textContent = new Date().getFullYear();

  const launchForm = document.getElementById('launch-form');
  const launchMessages = {
    en: { pending: 'Joining…', success: 'You’re on the list! We’ll let you know at launch.', error: 'We couldn’t save your request. Please try again later.', rate: 'Too many attempts. Please try again in an hour.', submit: 'Count me in' },
    tr: { pending: 'Ekleniyor…', success: 'Listeye katıldın! Oyun çıktığında haber vereceğiz.', error: 'Kaydını şu an alamadık. Lütfen daha sonra tekrar dene.', rate: 'Çok fazla deneme oldu. Lütfen bir saat sonra tekrar dene.', submit: 'Beni de ekle' }
  };
  launchForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (!launchForm.reportValidity()) return;
    const button = launchForm.querySelector('button');
    if (button.disabled) return;
    const notice = document.getElementById('launch-status');
    const messages = launchMessages[document.documentElement.lang] || launchMessages.en;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 12000);
    button.disabled = true; button.textContent = messages.pending;
    notice.textContent = ''; notice.className = 'form-status';
    try {
      const response = await fetch('/api/launch-list', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, signal: controller.signal,
        body: JSON.stringify({ email: launchForm.elements.email.value, consent: launchForm.elements.consent.checked, website: launchForm.elements.website.value, language: document.documentElement.lang })
      });
      const result = await response.json();
      if (!response.ok || !result.ok) throw new Error(result.error || 'unavailable');
      launchForm.reset(); notice.textContent = messages.success; notice.className = 'form-status success';
    } catch (error) {
      notice.textContent = error.message === 'rate' ? messages.rate : messages.error; notice.className = 'form-status error';
    } finally {
      clearTimeout(timeout); button.disabled = false;
      button.textContent = (launchMessages[document.documentElement.lang] || launchMessages.en).submit;
    }
  });

  // Navigation stays visible without JavaScript; only enhance after handlers are ready.
  const bar = document.querySelector('.bar');
  const menu = document.querySelector('.menu-toggle');
  const nav = document.getElementById('site-nav');
  const closeMenu = () => {
    bar.classList.remove('menu-open');
    menu.setAttribute('aria-expanded', 'false');
  };
  menu.addEventListener('click', () => {
    const open = menu.getAttribute('aria-expanded') !== 'true';
    menu.setAttribute('aria-expanded', String(open));
    bar.classList.toggle('menu-open', open);
  });
  nav.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && menu.getAttribute('aria-expanded') === 'true') {
      closeMenu();
      menu.focus();
    }
  });
  bar.classList.add('menu-ready');

  const tabs = [...document.querySelectorAll('[data-gallery-tab]')];
  const panels = [...document.querySelectorAll('[data-gallery-panel]')];
  function selectGallery(tab, focus = false) {
    tabs.forEach(item => {
      const selected = item === tab;
      item.setAttribute('aria-selected', String(selected));
      item.tabIndex = selected ? 0 : -1;
    });
    panels.forEach(panel => {
      panel.hidden = panel.dataset.galleryPanel !== tab.dataset.galleryTab;
      if (panel.hidden) panel.querySelectorAll('video').forEach(video => video.pause());
    });
    if (focus) tab.focus();
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => selectGallery(tab));
    tab.addEventListener('keydown', event => {
      let next;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
      if (event.key === 'Home') next = 0;
      if (event.key === 'End') next = tabs.length - 1;
      if (next !== undefined) {
        event.preventDefault();
        selectGallery(tabs[next], true);
      }
    });
  });
  panels.forEach(panel => {
    panel.setAttribute('role', 'tabpanel');
    panel.setAttribute('aria-labelledby', 'tab-' + panel.dataset.galleryPanel);
  });
  selectGallery(tabs[0]);
  document.querySelector('.gallery-tabs').hidden = false;

  // Screenshots remain normal image links without JavaScript or native dialog support.
  const screenshotDialog = document.getElementById('screenshot-dialog');
  if (typeof screenshotDialog.showModal === 'function') {
    const largeImage = screenshotDialog.querySelector('img');
    const closeScreenshot = () => screenshotDialog.close();
    screenshotDialog.querySelector('button').addEventListener('click', closeScreenshot);
    screenshotDialog.addEventListener('click', event => {
      if (event.target !== screenshotDialog) return;
      const box = screenshotDialog.getBoundingClientRect();
      if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) closeScreenshot();
    });
    screenshotDialog.addEventListener('close', () => largeImage.removeAttribute('src'));
    document.querySelectorAll('.zoom-shot').forEach(link => {
      link.setAttribute('aria-haspopup', 'dialog');
      link.addEventListener('click', event => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        const image = link.querySelector('img');
        largeImage.src = image.currentSrc || image.src;
        largeImage.alt = image.alt;
        document.getElementById('screenshot-title').textContent = link.closest('figure').querySelector('figcaption span').textContent;
        screenshotDialog.showModal();
      });
    });
  }

  // Oynanış videoları: sayfa açılışında hiçbiri inmez (preload=none). Ekranda en az %60 görünenlerden
  // yalnız biri oynar; bitince görünür olan sıradakine geçer, ekrandan çıkınca durur. Dokunma = oynat/durdur.
  // Hareketi azalt tercihinde kendiliğinden oynamaz, oynatma düğmeleri görünür.
  const clips = [...document.querySelectorAll("video.clip")];
  // Native controls provide keyboard and touch access, including when autoplay is unavailable.
  clips.forEach(video => video.controls = true);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) clips.forEach(video => video.pause());
  });
  if (clips.length) {
    if (matchMedia("(prefers-reduced-motion: reduce)").matches || !("IntersectionObserver" in window)) {
      clips.forEach(v => v.controls = true);
    } else {
      const ratio = new Map();
      let current = null;
      const visible = () => document.hidden ? [] : clips.filter(v => !v.closest("[hidden]") && (ratio.get(v) || 0) >= 0.6);
      const play = v => {
        if (current && current !== v) current.pause();
        current = v;
        if (v) v.play().catch(() => {});
      };
      const io = new IntersectionObserver(entries => {
        entries.forEach(e => ratio.set(e.target, e.intersectionRatio));
        if (!current || !visible().includes(current)) play(visible()[0] || null);
      }, { threshold: [0, 0.6, 1] });
      clips.forEach(v => {
        io.observe(v);
        v.addEventListener("ended", () => {
          const vs = visible();
          play(vs.length ? vs[(vs.indexOf(v) + 1) % vs.length] : null);
        });
        v.addEventListener("play", () => {
          clips.forEach(other => { if (other !== v) other.pause(); });
          current = v;
        });
      });
    }
  }
})();
