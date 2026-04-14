'use strict';

// ─── State ───────────────────────────────────────────────────────────────────
let currentSearchId = null;

// ─── DOM refs ────────────────────────────────────────────────────────────────
const statusBar   = document.getElementById('status-bar');
const resultsEl   = document.getElementById('results');
const playerBar   = document.getElementById('player-bar');
const audioEl     = document.getElementById('audio-el');
const playerTrack = document.getElementById('player-track');
const playerPeer  = document.getElementById('player-peer');
const errorBanner = document.getElementById('error-banner');
const errorText   = document.getElementById('error-text');

// ─── SignalR — stream hub (peer disconnect events) ───────────────────────────
const streamHub = new signalR.HubConnectionBuilder()
  .withUrl('/hub/stream')
  .withAutomaticReconnect()
  .build();

streamHub.on('streamError', ({ username, filename, reason }) => {
  showError(`Peer ${username} disconnected: ${reason}`);
});

streamHub.onreconnecting(() => setStatus('Reconnecting…'));
streamHub.onreconnected(() => setStatus('Connected'));
streamHub.onclose(() => setStatus('Disconnected'));

// ─── SignalR — search hub (live search results) ───────────────────────────────
const searchHub = new signalR.HubConnectionBuilder()
  .withUrl('/hub/search')
  .withAutomaticReconnect()
  .build();

searchHub.on('UPDATE', async (search) => {
  if (search.id !== currentSearchId) return;
  setStatus(`Search: ${search.fileCount} file${search.fileCount !== 1 ? 's' : ''} from ${search.responseCount} peer${search.responseCount !== 1 ? 's' : ''}`);

  // Responses only available via REST — fetch when the search completes
  if (search.isComplete) {
    await fetchAndRenderResponses(currentSearchId);
  }
});

// ─── Startup ─────────────────────────────────────────────────────────────────
async function start() {
  setStatus('Connecting…');
  try {
    await Promise.all([streamHub.start(), searchHub.start()]);
    setStatus('Ready');
  } catch (err) {
    setStatus(`Connection failed: ${err.message}`);
    setTimeout(start, 5000);
  }
}

start();

// ─── Search ──────────────────────────────────────────────────────────────────
document.getElementById('search-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const query = document.getElementById('search-input').value.trim();
  if (!query) return;

  resultsEl.innerHTML = '';
  currentSearchId = null;
  hideError();
  setStatus(`Searching for "${query}"…`);

  try {
    const resp = await fetch('/api/v0/searches', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ searchText: query }),
    });

    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const search = await resp.json();
    currentSearchId = search.id;
  } catch (err) {
    setStatus(`Search failed: ${err.message}`);
  }
});

// ─── Fetch all responses from REST API and render them ───────────────────────
async function fetchAndRenderResponses(searchId) {
  try {
    const resp = await fetch(`/api/v0/searches/${searchId}/responses`);
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const responses = await resp.json();
    resultsEl.innerHTML = '';
    for (const response of responses) {
      appendResponse(response);
    }
  } catch (err) {
    setStatus(`Failed to load results: ${err.message}`);
  }
}

// ─── Render a search response (one peer's results) ───────────────────────────
function appendResponse(response) {
  const { username, files = [] } = response;
  if (!files.length) return;

  // Group under a peer header
  let group = document.getElementById(`peer-${CSS.escape(username)}`);
  if (!group) {
    group = document.createElement('div');
    group.className = 'peer-group';
    group.id = `peer-${CSS.escape(username)}`;

    const header = document.createElement('div');
    header.className = 'peer-header';
    header.innerHTML = `<strong>${escHtml(username)}</strong>`;
    group.appendChild(header);
    resultsEl.appendChild(group);
  }

  for (const file of files) {
    const ext = (file.filename || '').split('.').pop().toUpperCase();
    const size = file.size ? `${(file.size / 1_048_576).toFixed(1)} MB` : '';
    const speed = file.uploadSpeed ? `${Math.round(file.uploadSpeed / 1024)} KB/s` : '';

    const row = document.createElement('div');
    row.className = 'result-row';
    row.innerHTML = `
      <button class="play-btn" title="Play" data-username="${escAttr(username)}" data-filename="${escAttr(file.filename)}">▶</button>
      <span class="filename" title="${escAttr(file.filename)}">${escHtml(baseName(file.filename))}</span>
      <span class="meta">${ext}${size ? ' · ' + size : ''}${speed ? ' · ' + speed : ''}</span>
    `;
    group.appendChild(row);
  }
}

// ─── Playback ─────────────────────────────────────────────────────────────────
resultsEl.addEventListener('click', (e) => {
  const btn = e.target.closest('.play-btn');
  if (!btn) return;

  const username = btn.dataset.username;
  const filename = btn.dataset.filename;
  playTrack(username, filename);
});

function playTrack(username, filename) {
  hideError();

  // Mark active button
  document.querySelectorAll('.play-btn.active').forEach(b => b.classList.remove('active'));
  const btn = document.querySelector(`.play-btn[data-username="${CSS.escape(username)}"][data-filename="${CSS.escape(filename)}"]`);
  if (btn) btn.classList.add('active');

  // Build stream URL — filename may contain slashes (path), encode each segment.
  // Pass JWT as access_token query param — browsers don't send custom headers on audio src requests.
  const encodedFilename = filename.split('/').map(encodeURIComponent).join('/');
  const streamUrl = `/api/v0/stream/${encodeURIComponent(username)}/${encodedFilename}`;

  audioEl.src = streamUrl;
  audioEl.load();
  audioEl.play().catch(() => {/* autoplay policy — user gesture already happened */});

  playerTrack.textContent = baseName(filename);
  playerPeer.textContent = `from ${username}`;
  playerBar.classList.add('visible');

  // Disable seeking — not supported in v1 (live from peer, no file on disk)
  audioEl.addEventListener('seeking', snapBack, { passive: true });
}

function snapBack() {
  // Safari fires seeking even on initial load; only snap if meaningfully ahead
  const dur = audioEl.duration;
  if (isFinite(dur) && Math.abs(audioEl.currentTime - dur) > 2) {
    audioEl.currentTime = 0;
  }
}

// ─── Error banner ─────────────────────────────────────────────────────────────
function showError(msg) {
  errorText.textContent = msg;
  errorBanner.classList.add('visible');
}

function hideError() {
  errorBanner.classList.remove('visible');
}

document.getElementById('error-dismiss').addEventListener('click', hideError);

// ─── Helpers ──────────────────────────────────────────────────────────────────
function setStatus(msg) { statusBar.textContent = msg; }

function baseName(path) {
  return (path || '').replace(/\\/g, '/').split('/').pop();
}

function escHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function escAttr(s) { return escHtml(s); }
