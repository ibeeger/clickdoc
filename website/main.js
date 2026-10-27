// ClickDoc site: language switch, copy button, hero demo, latest version.
(() => {
  "use strict";

  // ---------- Sponsor: set once GitHub Sponsors is approved, e.g. "https://github.com/sponsors/ibeeger" ----------
  const SPONSOR_URL = "";
  if (SPONSOR_URL) {
    document.querySelectorAll(".sponsor-link").forEach((a) => (a.href = SPONSOR_URL));
    document.querySelectorAll(".sponsor, .sponsor-nav").forEach((el) => (el.hidden = false));
  }

  // ---------- Nav turns solid once it leaves the gradient hero ----------
  const nav = document.querySelector(".nav");
  const hero = document.querySelector(".hero");
  const updateNav = () => nav && hero && nav.classList.toggle("solid", scrollY > hero.offsetHeight - 90);
  addEventListener("scroll", updateNav, { passive: true });
  updateNav();

  // ---------- i18n ----------
  // Chinese lives in the HTML; English replaces it on demand.
  const EN = {
    "nav.how": "How it works", "nav.parts": "Features", "nav.install": "Install", "nav.faq": "FAQ",
    "hero.badge": "Free & open source · macOS menu bar app",
    "cover.title": "Click through it once.<br>The guide <em>writes itself</em>.",
    "cover.lede": "Use your Mac as usual. ClickDoc screenshots every click, circles where you clicked and drafts each step. Tidy it up, then export to Markdown, Notion or PDF.",
    "cta.download": "Download for macOS", "cta.github": "Source on GitHub",
    "cta.spec": "macOS 14+ · Apple silicon & Intel",
    "doc.title": "How to change the system language",
    "demo.title": "System Settings", "demo.wifi": "Wi‑Fi", "demo.bt": "Bluetooth", "demo.general": "General",
    "demo.display": "Displays", "demo.privacy": "Privacy & Security", "demo.name": "Name", "demo.lang": "Language",
    "demo.chinese": "English", "demo.date": "Date & Time", "demo.done": "Done",
    "demo.step1": "Click “General” in System Settings",
    "demo.step2": "Click “English” in System Settings",
    "demo.step3": "Click “Done” in System Settings",
    "how.eyebrow": "How it works", "how.title": "A guide in three steps",
    "how.menu.start": "Start Recording", "how.menu.scope": "Capture Area ›", "how.menu.editor": "Open Editor",
    "how.1.title": "Record", "how.1.body": "Hit Start Recording in the menu bar and work as usual. Every click becomes a step: screenshot, red circle and caption in one go.",
    "how.edit.label": "Click here",
    "how.2.title": "Edit", "how.2.body": "Reword, reorder or bulk-delete steps. Point things out with arrows, boxes and text, and blur emails, keys and anything private.",
    "how.3.title": "Export", "how.3.body": "Export a Markdown folder, a Notion import package or a paginated A4 PDF. Blur and annotations are burned into the images.",
    "feat.eyebrow": "Features", "parts.title": "Everything you need, nothing you don't",
    "p1.t": "Click capture", "p1.d": "Screenshots every click and circles the spot in red. Double-clicks merge into one step; right-clicks count too.",
    "p2.t": "Window-only capture", "p2.d": "Crops to the window you clicked, keeping any menu it opened. Switch to the whole screen any time.",
    "p3.t": "Steps written for you", "p3.d": "Reads button and menu names to write captions like <q>Click <q>New Tab</q> in Safari</q>.",
    "p4.t": "Annotations", "p4.d": "Arrows, boxes and text you can select, move, resize and change later.",
    "p5.t": "Blur", "p5.d": "Drag over anything sensitive to blur it. Originals are kept and blur is applied on export, so you can adjust it any time.",
    "p6.t": "Autosaved drafts", "p6.d": "Every step and edit is saved as you go. Quit, reopen and carry on.",
    "p7.t": "Three exports", "p7.d": "Markdown folder, Notion import package (.zip) and paginated A4 PDF.",
    "p8.t": "Fully local", "p8.d": "No account, no network, no uploads. All source code is public.",
    "p9.t": "Chinese & English", "p9.d": "Follows your system language, and so do the generated captions.",
    "install.eyebrow": "Install · about 2 minutes", "install.title": "Install",
    "i1.t": "Download and drag to Applications",
    "i1.d": "Open <a href=\"https://github.com/ibeeger/clickdoc/releases/latest/download/ClickDoc.dmg\">ClickDoc.dmg</a> and drag ClickDoc onto the Applications folder.",
    "i2.t": "Let macOS open it",
    "i2.d": "ClickDoc isn't notarized by Apple, so macOS blocks it the first time. Open Terminal, paste this line and press Return:",
    "i2.copy": "Copy", "i2.copied": "Copied",
    "i2.alt": "Prefer not to use Terminal? Open ClickDoc once, then click “Open Anyway” at the bottom of System Settings › Privacy & Security.",
    "i3.t": "Grant two permissions",
    "i3.d": "The first time you click Start Recording, turn on Accessibility and Screen Recording in System Settings › Privacy & Security, then reopen ClickDoc.",
    "warn.body": "ClickDoc has no Dock icon. Once it's running, look for the round icon in the menu bar.",
    "faq.title": "Frequently asked questions",
    "q1": "macOS says it “can't verify the developer” or that the app “is damaged”",
    "a1": "That's how macOS treats every un-notarized app; the file is fine. ClickDoc is a personal open-source project without paid notarization. Run the command from install step 2 once.",
    "q2": "Are my screenshots uploaded?", "a2": "No. Recording, editing and exporting all happen on your Mac. The app has no network features and needs no account.",
    "q3": "Why Accessibility and Screen Recording?", "a3": "Accessibility lets ClickDoc notice clicks and read button names; Screen Recording lets it take screenshots. It needs both.",
    "q4": "Do I need to grant access again after updating?", "a4": "Possibly. If an old ClickDoc is still listed under Accessibility, remove it with “−” and grant access again.",
    "q5": "Does it cost anything? Windows?", "a5": "Free and MIT-licensed. macOS 14 or later only, on Apple silicon or Intel.",
    "q7": "How is it different from Folge?",
    "a7": "Same idea: record clicks, get a step-by-step guide. Folge does more, such as Word and PowerPoint export, AI-written captions and brand templates, and its full version is a one-time $89. ClickDoc is lighter, entirely free and open source, has a Chinese UI and exports a Notion import package. ClickDoc is not affiliated with Folge.",
    "q6": "Found a bug or have an idea?", "a6": "Tell us on <a href=\"https://github.com/ibeeger/clickdoc/issues\" rel=\"noopener\">GitHub Issues</a>.",
    "closing.title": "Your next guide is one click-through away", "footer.feedback": "Feedback",
    "sponsor.title": "Enjoying ClickDoc?", "sponsor.nav": "Sponsor",
    "sponsor.body": "It's free and open source, and always will be. If it saved you some time, you can support its development through GitHub Sponsors.",
    "sponsor.button": "Sponsor on GitHub",
  };
  const TITLE = { zh: document.title, en: "ClickDoc · Click through it once. The guide writes itself." };

  const zhText = new Map();
  document.querySelectorAll("[data-i18n], [data-i18n-html]").forEach((el) => zhText.set(el, el.innerHTML));

  function applyLanguage(lang) {
    document.documentElement.lang = lang === "en" ? "en" : "zh-CN";
    zhText.forEach((zh, el) => {
      const key = el.dataset.i18n || el.dataset.i18nHtml;
      if (lang === "en" && EN[key] !== undefined) {
        if (el.dataset.i18nHtml !== undefined) el.innerHTML = EN[key];
        else el.textContent = EN[key];
      } else {
        el.innerHTML = zh;
      }
    });
    document.title = TITLE[lang] || TITLE.zh;
    document.querySelectorAll(".lang button").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.lang === lang)));
    currentLang = lang;
    demo.reset();
  }

  function initialLanguage() {
    const fromUrl = new URLSearchParams(location.search).get("lang");
    if (fromUrl === "en" || fromUrl === "zh") return fromUrl;
    try {
      const saved = localStorage.getItem("clickdoc.lang");
      if (saved === "en" || saved === "zh") return saved;
    } catch (_) { /* storage may be unavailable */ }
    return (navigator.languages || [navigator.language || ""]).some((l) => /^zh/i.test(l)) ? "zh" : "en";
  }

  let currentLang = "zh";
  document.querySelectorAll(".lang button").forEach((b) =>
    b.addEventListener("click", () => {
      applyLanguage(b.dataset.lang);
      try { localStorage.setItem("clickdoc.lang", b.dataset.lang); } catch (_) {}
    }));

  // ---------- Copy button ----------
  document.querySelectorAll("[data-copy]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const text = document.querySelector(btn.dataset.copy).textContent.trim();
      try {
        await navigator.clipboard.writeText(text);
      } catch (_) {
        const range = document.createRange();
        range.selectNodeContents(document.querySelector(btn.dataset.copy));
        const sel = getSelection(); sel.removeAllRanges(); sel.addRange(range);
        document.execCommand("copy");
        sel.removeAllRanges();
      }
      btn.textContent = currentLang === "en" ? EN["i2.copied"] : "已复制";
      btn.classList.add("done");
      setTimeout(() => {
        btn.textContent = currentLang === "en" ? EN["i2.copy"] : "复制";
        btn.classList.remove("done");
      }, 1600);
    });
  });

  // ---------- Hero demo: cursor clicks three targets, steps appear ----------
  const demo = (() => {
    const win = document.querySelector(".window");
    const ring = document.querySelector(".ring");
    const cursor = document.querySelector(".cursor");
    const steps = [...document.querySelectorAll(".log li")];
    const targets = [1, 2, 3].map((n) => document.querySelector(".target-" + n));
    const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;
    let timers = [];
    let generation = 0;

    const later = (ms, fn) => timers.push(setTimeout(fn, ms));
    const center = (el) => {
      const a = win.getBoundingClientRect(), b = el.getBoundingClientRect();
      return { x: b.left - a.left + b.width / 2, y: b.top - a.top + b.height / 2 };
    };
    const place = (el, p, dx = 0, dy = 0) => { el.style.transform = `translate(${p.x + dx}px, ${p.y + dy}px)`; };

    function showAll() {
      steps.forEach((s) => s.classList.add("show"));
      const p = center(targets[2]);
      ring.style.left = p.x + "px"; ring.style.top = p.y + "px";
      ring.style.opacity = 1; ring.style.transform = "none";
      targets[2].classList.add("is-hit");
      cursor.style.display = "none";
    }

    function run() {
      const gen = ++generation;
      steps.forEach((s) => s.classList.remove("show"));
      targets.forEach((t) => t.classList.remove("is-hit"));
      ring.classList.remove("pop");
      place(cursor, { x: win.clientWidth * 0.82, y: win.clientHeight * 0.85 });
      let t = 600;
      targets.forEach((target, i) => {
        later(t, () => { if (gen === generation) place(cursor, center(target), -3, -2); });
        t += 850;
        later(t, () => {
          if (gen !== generation) return;
          targets.forEach((x) => x.classList.remove("is-hit"));
          target.classList.add("is-hit");
          const p = center(target);
          ring.style.left = p.x + "px"; ring.style.top = p.y + "px";
          ring.classList.remove("pop"); void ring.offsetWidth; ring.classList.add("pop");
          steps[i].classList.add("show");
        });
        t += 1100;
      });
      later(t + 2200, () => { if (gen === generation) run(); });
    }

    function reset() {
      timers.forEach(clearTimeout); timers = [];
      if (!win) return;
      if (reduced) showAll(); else run();
    }

    // Pause when off-screen to save battery.
    if ("IntersectionObserver" in window && win && !reduced) {
      new IntersectionObserver(([entry]) => {
        if (entry.isIntersecting) reset();
        else { timers.forEach(clearTimeout); timers = []; generation++; }
      }).observe(win);
    }
    addEventListener("resize", () => { clearTimeout(reset.t); reset.t = setTimeout(reset, 200); });
    return { reset };
  })();

  applyLanguage(initialLanguage());

  // ---------- Latest version from GitHub (falls back to the baked-in text) ----------
  fetch("https://api.github.com/repos/ibeeger/clickdoc/releases/latest", { headers: { Accept: "application/vnd.github+json" } })
    .then((r) => (r.ok ? r.json() : null))
    .then((release) => {
      if (!release || !release.tag_name) return;
      const v = release.tag_name.startsWith("v") ? release.tag_name : "v" + release.tag_name;
      document.querySelectorAll("#version, #version-footer").forEach((el) => (el.textContent = v));
    })
    .catch(() => {});
})();
