(function () {
  'use strict';

  var HISTORY_LIMIT = 6;
  var workspace = window.location.pathname.match(/^\/w\/[a-z0-9-]+\//);
  var API = (workspace ? workspace[0] : '/') + 'api/';
  var log = document.getElementById('log');
  var empty = document.getElementById('empty');
  var form = document.getElementById('form');
  var input = document.getElementById('question');
  var sendButton = document.getElementById('send');
  var panel = document.getElementById('panel');
  var panelTitle = document.getElementById('panel-title');
  var panelPage = document.getElementById('panel-page');
  var panelBody = document.getElementById('panel-body');
  var history = [];
  var busy = false;

  document.getElementById('reset').addEventListener('click', function () {
    if (busy) { return; }
    history = [];
    log.querySelectorAll('.msg').forEach(function (node) { node.remove(); });
    empty.hidden = false;
    closePanel();
  });
  document.getElementById('panel-close').addEventListener('click', closePanel);

  input.addEventListener('keydown', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      form.requestSubmit();
    }
  });
  input.addEventListener('input', function () {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 160) + 'px';
  });

  form.addEventListener('submit', function (event) {
    event.preventDefault();
    var question = input.value.trim();
    if (!question || busy) { return; }
    input.value = '';
    input.style.height = 'auto';
    ask(question);
  });

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) { node.className = className; }
    if (text !== undefined) { node.textContent = text; }
    return node;
  }

  function scrollDown() { log.scrollTop = log.scrollHeight; }

  function setBusy(value) {
    busy = value;
    sendButton.disabled = value;
  }

  async function ask(question) {
    empty.hidden = true;
    var userMessage = el('div', 'msg user');
    userMessage.appendChild(el('div', 'bubble', question));
    log.appendChild(userMessage);

    var message = el('div', 'msg assistant');
    var status = el('div', 'status', 'Searching your documents…');
    var answer = el('div', 'answer');
    var sources = el('div', 'sources');
    message.appendChild(status);
    message.appendChild(answer);
    message.appendChild(sources);
    log.appendChild(message);
    scrollDown();

    var state = { text: '', citations: [] };
    setBusy(true);

    try {
      var response = await fetch(API + 'chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question: question, history: history.slice(-HISTORY_LIMIT) })
      });

      if (!response.ok) {
        var body = await response.json().catch(function () { return {}; });
        throw new Error(body.error || ('The request failed (' + response.status + ').'));
      }

      await readEvents(response.body, function (type, data) {
        if (type === 'sources') {
          state.citations = data;
          status.textContent = data.length === 0
            ? 'No matching passages…'
            : 'Found ' + data.length + (data.length === 1 ? ' passage' : ' passages') + '. Writing the answer…';
        } else if (type === 'delta') {
          status.hidden = true;
          state.text += data;
          renderAnswer(answer, state);
          scrollDown();
        } else if (type === 'declined') {
          status.hidden = true;
          state.text = data;
          state.citations = [];
          renderAnswer(answer, state);
        } else if (type === 'error') {
          throw new Error(data.error || 'Something went wrong.');
        }
      });

      status.hidden = true;
      renderAnswer(answer, state);
      renderSources(sources, state);
      history.push({ role: 'user', text: question });
      if (state.text) { history.push({ role: 'assistant', text: state.text }); }
    } catch (error) {
      status.hidden = true;
      message.appendChild(el('div', 'error', error.message));
    } finally {
      setBusy(false);
      scrollDown();
      input.focus();
    }
  }

  async function readEvents(stream, onEvent) {
    var reader = stream.getReader();
    var decoder = new TextDecoder();
    var buffer = '';
    while (true) {
      var chunk = await reader.read();
      if (chunk.done) { break; }
      buffer += decoder.decode(chunk.value, { stream: true });
      var parts = buffer.split('\n\n');
      buffer = parts.pop();
      parts.forEach(function (raw) { dispatch(raw, onEvent); });
    }
  }

  function dispatch(raw, onEvent) {
    var type = 'message';
    var data = [];
    raw.split('\n').forEach(function (line) {
      if (line.indexOf('event:') === 0) { type = line.slice(6).trim(); }
      else if (line.indexOf('data:') === 0) { data.push(line.slice(5).replace(/^ /, '')); }
    });
    if (data.length === 0) { return; }
    onEvent(type, JSON.parse(data.join('\n')));
  }

  function renderAnswer(container, state) {
    container.textContent = '';
    var known = {};
    state.citations.forEach(function (c) { known[c.number] = c; });
    var pattern = /\[(\d+(?:\s*[,;]\s*\d+)*)\]/g;
    var last = 0;
    var match;
    while ((match = pattern.exec(state.text)) !== null) {
      var numbers = match[1].split(/[,;]/).map(function (n) { return parseInt(n, 10); });
      if (!numbers.every(function (n) { return known[n]; })) { continue; }
      container.appendChild(document.createTextNode(state.text.slice(last, match.index)));
      numbers.forEach(function (n) { container.appendChild(citeButton(known[n])); });
      last = match.index + match[0].length;
    }
    container.appendChild(document.createTextNode(state.text.slice(last)));
  }

  function citeButton(citation) {
    var button = el('button', 'cite', String(citation.number));
    button.type = 'button';
    button.title = citation.title;
    button.addEventListener('click', function () { openSource(citation); });
    return button;
  }

  function renderSources(container, state) {
    container.textContent = '';
    var cited = {};
    var pattern = /\[(\d+(?:\s*[,;]\s*\d+)*)\]/g;
    var match;
    while ((match = pattern.exec(state.text)) !== null) {
      match[1].split(/[,;]/).forEach(function (n) { cited[parseInt(n, 10)] = true; });
    }
    state.citations.forEach(function (citation) {
      var card = el('button', 'source' + (cited[citation.number] ? '' : ' uncited'));
      card.type = 'button';
      var head = el('div', 'head');
      head.appendChild(el('span', 'n', '[' + citation.number + ']'));
      head.appendChild(document.createTextNode(citation.title));
      card.appendChild(head);
      card.appendChild(el('div', 'preview', citation.preview));
      card.addEventListener('click', function () { openSource(citation); });
      container.appendChild(card);
    });
  }

  async function openSource(citation) {
    var query = new URLSearchParams({ offset: String(citation.offset), length: String(citation.length) });
    if (citation.page !== null && citation.page !== undefined) { query.set('page', String(citation.page)); }

    panel.hidden = false;
    panelTitle.textContent = citation.title;
    panelPage.textContent = citation.page ? 'Page ' + citation.page : '';
    panelBody.textContent = 'Loading…';

    try {
      var response = await fetch(API + 'sources/' + citation.documentId + '?' + query.toString());
      if (!response.ok) { throw new Error('The original is not available.'); }
      var preview = await response.json();
      panelBody.textContent = '';
      var cutStart = preview.truncation === 'start' || preview.truncation === 'both';
      var cutEnd = preview.truncation === 'end' || preview.truncation === 'both';
      panelBody.appendChild(document.createTextNode((cutStart ? '…\n' : '') + preview.before));
      var mark = document.createElement('mark');
      mark.textContent = preview.highlight;
      panelBody.appendChild(mark);
      panelBody.appendChild(document.createTextNode(preview.after + (cutEnd ? '\n…' : '')));
      mark.scrollIntoView({ block: 'center' });
    } catch (error) {
      panelBody.textContent = error.message;
    }
  }

  function closePanel() { panel.hidden = true; }
})();
