const state = {
  host: null,
  layout: null,
  telemetry: null,
  token: null,
  authorized: false,
  prefs: { speedUnit: "kph", angleUnit: "deg" }
};

const fieldCatalog = [
  { field: "speed", label: "Speed" },
  { field: "gear", label: "Gear" },
  { field: "rpm", label: "RPM" },
  { field: "angle", label: "Angle" },
  { field: "torque", label: "Torque" },
  { field: "velocity", label: "Velocity" },
  { field: "clipping", label: "Clipping" },
  { field: "rate", label: "Telemetry Hz" },
  { field: "loopdt", label: "Loop dt" },
  { field: "throttle", label: "Throttle" },
  { field: "clutch", label: "Clutch" },
  { field: "brake", label: "Brake" },
  { field: "status", label: "Status" }
];

const widgetRefs = new Map();
const history = {};

const qs = (sel, root = document) => root.querySelector(sel);
const qsa = (sel, root = document) => Array.from(root.querySelectorAll(sel));

function setBanner(text, visible) {
  const banner = qs("#banner");
  if (visible) {
    banner.textContent = text;
    banner.classList.remove("hidden");
  } else {
    banner.classList.add("hidden");
  }
}

function applyTab(tabId) {
  qsa(".tab-btn").forEach(btn => {
    btn.classList.toggle("active", btn.dataset.tab === tabId);
  });
  qsa(".tab").forEach(tab => {
    tab.classList.toggle("active", tab.id === tabId);
  });
}

function formatValue(field, value) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return "N/A";
  }
  switch (field) {
    case "speed": {
      const unit = state.prefs.speedUnit === "mph" ? "mph" : "km/h";
      const val = state.prefs.speedUnit === "mph" ? value * 0.621371 : value;
      return `${val.toFixed(1)} ${unit}`;
    }
    case "angle":
      if (state.prefs.angleUnit === "norm") {
        return value.toFixed(2);
      }
      return `${value.toFixed(0)}°`;
    case "torque":
      return value.toFixed(0);
    case "velocity":
      return value.toFixed(1);
    case "clipping":
      return `${value.toFixed(0)}%`;
    case "loopdt":
      return `${value.toFixed(2)} ms`;
    case "rate":
      return value.toFixed(0);
    case "throttle":
    case "clutch":
    case "brake":
      return `${(value * 100).toFixed(0)}%`;
    default:
      return value.toString();
  }
}

function getFieldValue(field, frame) {
  if (!frame) return null;
  switch (field) {
    case "speed": return frame.vehicleSpeed;
    case "gear": return frame.gear;
    case "rpm": return frame.rpm;
    case "angle": return state.prefs.angleUnit === "norm" ? frame.wheelAngleNorm : frame.wheelAngle;
    case "torque": return frame.torqueCommand;
    case "velocity": return frame.wheelVelocity;
    case "clipping": return frame.clippingPercent;
    case "rate": return frame.telemetryRateHz;
    case "loopdt": return frame.loopDtMs;
    case "throttle": return frame.throttle;
    case "clutch": return frame.clutch;
    case "brake": return frame.brake;
    case "status":
      return `${frame.isConnected ? "Connected" : "Disconnected"} | ${frame.calibrationStatus} | ${frame.saveStatus}`;
    default:
      return null;
  }
}

function buildWidget(widget) {
  const el = document.createElement("div");
  el.className = "widget";
  el.dataset.widgetId = widget.id;
  el.style.gridColumn = `${widget.x + 1} / span ${widget.w}`;
  el.style.gridRow = `${widget.y + 1} / span ${widget.h}`;

  const label = document.createElement("div");
  label.className = "widget-label";
  label.textContent = widget.label || widget.field;

  const value = document.createElement("div");
  value.className = widget.type === "status" ? "widget-status" : "widget-value";
  value.textContent = "—";

  el.appendChild(label);

  if (widget.type === "bar") {
    el.appendChild(value);
    const bar = document.createElement("div");
    bar.className = "bar";
    const fill = document.createElement("div");
    fill.className = "bar-fill";
    fill.style.width = "0%";
    bar.appendChild(fill);
    el.appendChild(bar);
    widgetRefs.set(widget.id, { widget, value, fill, label });
  } else if (widget.type === "graph") {
    const canvas = document.createElement("canvas");
    el.appendChild(canvas);
    widgetRefs.set(widget.id, { widget, canvas, label });
  } else {
    el.appendChild(value);
    widgetRefs.set(widget.id, { widget, value, label });
  }

  return el;
}

function renderLayout() {
  if (!state.layout) return;
  const pages = state.layout.pages || [];
  const containers = {
    drive: qs("#drive-grid"),
    ffb: qs("#ffb-grid"),
    diag: qs("#diag-grid")
  };

  Object.values(containers).forEach(c => {
    if (c) c.innerHTML = "";
  });
  widgetRefs.clear();

  pages.forEach(page => {
    const container = containers[page.id];
    if (!container) return;
    container.style.gridTemplateColumns = `repeat(${state.layout.columns || 12}, 1fr)`;
    page.widgets.forEach(widget => {
      container.appendChild(buildWidget(widget));
    });
  });

  populateLayoutEditor();
}

function updateWidgets(frame) {
  widgetRefs.forEach(ref => {
    const { widget } = ref;
    if (widget.type === "graph") {
      updateGraph(ref, frame);
      return;
    }
    const value = getFieldValue(widget.field, frame);
    if (widget.type === "bar") {
      const percent = widget.field === "clipping"
        ? Math.min(100, Math.max(0, value || 0))
        : Math.min(100, Math.max(0, (value || 0) * 100));
      ref.fill.style.width = `${percent}%`;
      if (ref.value) {
        ref.value.textContent = formatValue(widget.field, value);
      }
    } else if (widget.type === "status") {
      ref.value.textContent = value ?? "—";
    } else if (ref.value) {
      ref.value.textContent = formatValue(widget.field, value);
    }
  });
}

function updateGraph(ref, frame) {
  if (!frame) return;
  const field = ref.widget.field;
  const value = getFieldValue(field, frame);
  if (value === null || value === undefined) return;
  if (!history[field]) history[field] = [];
  const now = Date.now();
  history[field].push({ t: now, v: value });
  history[field] = history[field].filter(p => now - p.t <= 10000);
  const canvas = ref.canvas;
  const ctx = canvas.getContext("2d");
  const rect = canvas.getBoundingClientRect();
  canvas.width = rect.width * devicePixelRatio;
  canvas.height = rect.height * devicePixelRatio;
  ctx.scale(devicePixelRatio, devicePixelRatio);
  ctx.clearRect(0, 0, rect.width, rect.height);
  const points = history[field];
  if (points.length < 2) return;
  const values = points.map(p => p.v);
  let min = Math.min(...values);
  let max = Math.max(...values);
  if (Math.abs(max - min) < 1) {
    max += 1;
    min -= 1;
  }
  ctx.beginPath();
  points.forEach((p, i) => {
    const x = ((p.t - points[0].t) / 10000) * rect.width;
    const y = rect.height - ((p.v - min) / (max - min)) * rect.height;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  });
  ctx.strokeStyle = "#2f7f8a";
  ctx.lineWidth = 2;
  ctx.stroke();
}

async function fetchState() {
  const res = await fetch("/api/state");
  state.host = await res.json();
  state.authorized = state.host.authorized;
  updateHeader();
  updateControlState();
  if (state.host.currentTuning) {
    setSliderValue("strength", state.host.currentTuning.strength);
    setSliderValue("damping", state.host.currentTuning.damping);
    setSliderValue("friction", state.host.currentTuning.friction);
    setSliderValue("inertia", state.host.currentTuning.inertia);
  }
  if (state.host.safeCaps) {
    setSliderMax("strength", state.host.safeCaps.strength);
    setSliderMax("damping", state.host.safeCaps.damping);
    setSliderMax("friction", state.host.safeCaps.friction);
    setSliderMax("inertia", state.host.safeCaps.inertia);
  }
  if (state.host.requirePin && !state.authorized) {
    showPinModal(true);
  } else {
    showPinModal(false);
  }
}

async function fetchLayout() {
  const res = await fetch("/api/layout");
  state.layout = await res.json();
  state.layout.columns = state.layout.columns || 12;
  state.layout.pages = state.layout.pages || [];
  state.layout.preferences = state.layout.preferences || { speedUnit: "kph", angleUnit: "deg" };
  state.prefs.speedUnit = state.layout.preferences.speedUnit || "kph";
  state.prefs.angleUnit = state.layout.preferences.angleUnit || "deg";
  renderLayout();
  updateLayoutInputs();
}

async function fetchProfiles() {
  const res = await fetch("/api/profiles");
  const list = await res.json();
  const select = qs("#profile-select");
  select.innerHTML = "";
  list.forEach(name => {
    const opt = document.createElement("option");
    opt.value = name;
    opt.textContent = name;
    select.appendChild(opt);
  });
}

function updateHeader() {
  const conn = qs("#conn-pill");
  const cal = qs("#cal-pill");
  const save = qs("#save-pill");
  const connected = state.host.connection === "Connected";
  conn.textContent = state.host.connection;
  conn.className = connected ? "pill ok" : "pill warn";
  cal.textContent = state.host.calibration;
  cal.className = state.host.calibration === "Calibrated" ? "pill ok" : "pill warn";
  save.textContent = state.host.saveStatus;
  if (state.host.saveStatus && state.host.saveStatus.toLowerCase().includes("unsaved")) {
    save.className = "pill warn";
  } else if (state.host.saveStatus && state.host.saveStatus.toLowerCase().includes("saved")) {
    save.className = "pill ok";
  } else {
    save.className = "pill";
  }

  if (!connected) {
    setBanner("Wheel disconnected. Telemetry and controls are paused.", true);
  } else if (state.host.calibration !== "Calibrated") {
    setBanner("Calibration required. Controls are limited until calibration.", true);
  } else {
    setBanner("", false);
  }
}

function updateControlState() {
  const disabled = state.host.calibration !== "Calibrated" && !state.host.advancedRemote;
  qs("#apply-tuning").disabled = disabled || !state.authorized;
  qs("#apply-profile").disabled = disabled || !state.authorized;
  qs("#profile-select").disabled = disabled || !state.authorized;
  ["#strength-slider", "#damping-slider", "#friction-slider", "#inertia-slider"].forEach(sel => {
    qs(sel).disabled = disabled || !state.authorized;
  });
  qs("#ffb-lock-hint").textContent = disabled
    ? "Controls locked until calibration or Advanced Remote is enabled from desktop."
    : state.authorized ? "" : "Enter PIN to unlock controls.";
}

function showPinModal(show) {
  qs("#pin-modal").classList.toggle("hidden", !show);
}

async function authenticate(pin) {
  const res = await fetch("/api/auth", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ pin })
  });
  if (!res.ok) {
    return false;
  }
  const data = await res.json();
  state.token = data.token;
  state.authorized = true;
  return true;
}

async function sendControl(payload) {
  const headers = { "Content-Type": "application/json" };
  if (state.token) {
    headers["X-CSRF-Token"] = state.token;
  }
  const res = await fetch("/api/control", {
    method: "POST",
    headers,
    body: JSON.stringify(payload)
  });
  if (!res.ok) {
    const msg = await res.text();
    throw new Error(msg || "Control failed");
  }
}

async function saveLayout() {
  const headers = { "Content-Type": "application/json" };
  if (state.token) {
    headers["X-CSRF-Token"] = state.token;
  }
  const res = await fetch("/api/layout", {
    method: "POST",
    headers,
    body: JSON.stringify(state.layout)
  });
  if (!res.ok) {
    throw new Error("Layout save failed");
  }
}

function setSliderValue(key, value) {
  const slider = qs(`#${key}-slider`);
  const label = qs(`#${key}-val`);
  slider.value = value;
  label.textContent = value;
}

function setSliderMax(key, value) {
  const slider = qs(`#${key}-slider`);
  slider.max = value;
}

function initSliders() {
  ["strength", "damping", "friction", "inertia"].forEach(key => {
    const slider = qs(`#${key}-slider`);
    const label = qs(`#${key}-val`);
    slider.addEventListener("input", () => {
      label.textContent = slider.value;
    });
  });
}

function initTabs() {
  qsa(".tab-btn").forEach(btn => {
    btn.addEventListener("click", () => applyTab(btn.dataset.tab));
  });
}

function initLayoutEditor() {
  const pageSelect = qs("#layout-page");
  pageSelect.addEventListener("change", populateLayoutEditor);
  qs("#save-layout").addEventListener("click", async () => {
    try {
      await saveLayout();
    } catch (err) {
      alert(err.message);
    }
  });
  qs("#unit-speed").addEventListener("change", () => {
    state.prefs.speedUnit = qs("#unit-speed").value;
    state.layout.preferences = state.layout.preferences || {};
    state.layout.preferences.speedUnit = state.prefs.speedUnit;
  });
  qs("#unit-angle").addEventListener("change", () => {
    state.prefs.angleUnit = qs("#unit-angle").value;
    state.layout.preferences = state.layout.preferences || {};
    state.layout.preferences.angleUnit = state.prefs.angleUnit;
  });
  qs("#add-widget").addEventListener("click", () => {
    const page = getSelectedPage();
    if (!page) return;
    const type = qs("#new-widget-type").value;
    const field = qs("#new-widget-field").value;
    const label = qs("#new-widget-label").value || field;
    page.widgets.push({
      id: `${Date.now()}`,
      type,
      field,
      label,
      x: 0,
      y: page.widgets.length * 2,
      w: 4,
      h: 2
    });
    populateLayoutEditor();
    renderLayout();
  });
  qs("#export-layout").addEventListener("click", () => {
    const data = JSON.stringify(state.layout, null, 2);
    const blob = new Blob([data], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "dashboard-layout.json";
    a.click();
    URL.revokeObjectURL(url);
  });
  qs("#import-layout").addEventListener("change", async e => {
    const file = e.target.files[0];
    if (!file) return;
    const text = await file.text();
    try {
      const parsed = JSON.parse(text);
      state.layout = parsed;
      state.layout.preferences = state.layout.preferences || { speedUnit: "kph", angleUnit: "deg" };
      renderLayout();
      updateLayoutInputs();
      await saveLayout();
    } catch (err) {
      alert("Invalid layout file.");
    }
  });
}

function updateLayoutInputs() {
  if (!state.layout) return;
  qs("#unit-speed").value = state.layout.preferences?.speedUnit || "kph";
  qs("#unit-angle").value = state.layout.preferences?.angleUnit || "deg";
  const pageSelect = qs("#layout-page");
  pageSelect.innerHTML = "";
  state.layout.pages.forEach(page => {
    const opt = document.createElement("option");
    opt.value = page.id;
    opt.textContent = page.name;
    pageSelect.appendChild(opt);
  });
  populateLayoutEditor();
}

function getSelectedPage() {
  const pageId = qs("#layout-page").value;
  return state.layout.pages.find(p => p.id === pageId);
}

function populateLayoutEditor() {
  if (!state.layout) return;
  const list = qs("#widget-list");
  list.innerHTML = "";
  const page = getSelectedPage();
  if (!page) return;
  page.widgets.forEach(widget => {
    const container = document.createElement("div");
    container.className = "widget-editor";

    const row1 = document.createElement("div");
    row1.className = "row";
    const label = document.createElement("input");
    label.type = "text";
    label.value = widget.label;
    label.addEventListener("input", () => {
      widget.label = label.value;
      const target = document.querySelector(`[data-widget-id="${widget.id}"] .widget-label`);
      if (target) target.textContent = widget.label || widget.field;
    });
    const remove = document.createElement("button");
    remove.className = "secondary";
    remove.textContent = "Remove";
    remove.addEventListener("click", () => {
      page.widgets = page.widgets.filter(w => w.id !== widget.id);
      renderLayout();
      populateLayoutEditor();
    });
    row1.appendChild(label);
    row1.appendChild(remove);

    const row2 = document.createElement("div");
    row2.className = "row";
    row2.appendChild(buildNumberInput("X", widget.x, value => { widget.x = value; renderLayout(); }));
    row2.appendChild(buildNumberInput("Y", widget.y, value => { widget.y = value; renderLayout(); }));
    row2.appendChild(buildNumberInput("W", widget.w, value => { widget.w = value; renderLayout(); }));
    row2.appendChild(buildNumberInput("H", widget.h, value => { widget.h = value; renderLayout(); }));

    container.appendChild(row1);
    container.appendChild(row2);
    list.appendChild(container);
  });

  const fieldSelect = qs("#new-widget-field");
  fieldSelect.innerHTML = "";
  fieldCatalog.forEach(item => {
    const opt = document.createElement("option");
    opt.value = item.field;
    opt.textContent = item.label;
    fieldSelect.appendChild(opt);
  });
}

function buildNumberInput(labelText, value, onChange) {
  const wrapper = document.createElement("div");
  wrapper.className = "row";
  const label = document.createElement("label");
  label.textContent = labelText;
  const input = document.createElement("input");
  input.type = "number";
  input.value = value;
  input.min = 0;
  input.addEventListener("change", () => {
    const v = parseInt(input.value, 10);
    onChange(Number.isNaN(v) ? 0 : v);
  });
  wrapper.appendChild(label);
  wrapper.appendChild(input);
  return wrapper;
}

function initPinModal() {
  qs("#pin-submit").addEventListener("click", async () => {
    const pin = qs("#pin-input").value;
    const ok = await authenticate(pin);
    if (!ok) {
      qs("#pin-error").textContent = "Incorrect PIN.";
      return;
    }
    qs("#pin-error").textContent = "";
    showPinModal(false);
    await fetchState();
  });
}

function initControls() {
  qs("#apply-tuning").addEventListener("click", async () => {
    try {
      await sendControl({
        action: "tuning",
        strength: parseInt(qs("#strength-slider").value, 10),
        damping: parseInt(qs("#damping-slider").value, 10),
        friction: parseInt(qs("#friction-slider").value, 10),
        inertia: parseInt(qs("#inertia-slider").value, 10)
      });
    } catch (err) {
      alert(err.message);
    }
  });
  qs("#apply-profile").addEventListener("click", async () => {
    try {
      await sendControl({
        action: "profile",
        name: qs("#profile-select").value
      });
    } catch (err) {
      alert(err.message);
    }
  });
}

function initSse() {
  const es = new EventSource("/api/telemetry");
  es.onmessage = event => {
    const frame = JSON.parse(event.data);
    state.telemetry = frame;
    updateWidgets(frame);
    qs("#diag-rate").textContent = frame.telemetryRateHz.toFixed(0);
    qs("#diag-loopdt").textContent = `${frame.loopDtMs.toFixed(2)} ms`;
    qs("#diag-clip").textContent = `${frame.clippingPercent.toFixed(0)}%`;
    qs("#diag-sim").textContent = frame.simProvider || "None";
  };
  es.onerror = () => {
    // retry silently
  };
}

async function init() {
  initTabs();
  initSliders();
  initLayoutEditor();
  initPinModal();
  initControls();

  await fetchState();
  await fetchLayout();
  await fetchProfiles();
  initSse();

  setInterval(fetchState, 5000);
}

init();
