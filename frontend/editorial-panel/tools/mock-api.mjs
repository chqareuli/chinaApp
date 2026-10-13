import { createServer } from 'node:http';

const editor = {
  id: 1,
  email: 'admin@chinese.ge',
  displayName: 'ნინო რედაქტორი',
  role: 'SuperAdmin',
  isActive: true,
  createdAtUtc: '2026-01-10T10:00:00Z',
};
const entryTemplate = {
  id: 17,
  lemma: '学习',
  pinyin: 'xuéxí',
  pinyins: [{ id: 1, value: 'xuéxí', order: 0 }],
  complexForms: [{ id: 1, value: '學習', order: 0 }],
  status: 'ZhReview',
  mainAuthorDisplayName: 'ანა დამხმარე',
  lastEditorDisplayName: 'ნინო რედაქტორი',
  createdAtUtc: '2026-07-18T08:00:00Z',
  updatedAtUtc: '2026-07-20T06:20:00Z',
  rowVersion: 12,
  homonyms: [
    {
      text: '学习',
      senses: [
        {
          number: 1,
          definition: 'სწავლა; ცოდნის შეძენა',
          examples: [{ zh: '我学习中文。', ka: 'მე ჩინურს ვსწავლობ.' }],
        },
      ],
    },
  ],
  capabilities: {
    canEditContent: true,
    allowedStatusTransitions: ['NewEntry', 'KaReview', 'Ready', 'Published', 'Archived'],
  },
};
let entry = null;
let nextEntryId = 1;
let comments = [
  {
    id: 1,
    entryId: 17,
    authorEditorId: 2,
    authorDisplayName: 'ანა დამხმარე',
    commentText: 'გთხოვთ, გადაამოწმოთ მეორე მაგალითის თარგმანი.',
    status: 'Active',
    createdAtUtc: '2026-07-20T05:00:00Z',
    updatedAtUtc: '2026-07-20T05:00:00Z',
  },
];
let supportMaterials = [
  {
    id: 1,
    entryId: 17,
    language: 'Chinese',
    commentText: '现代汉语词典 — 学习',
    createdAtUtc: '2026-07-20T05:20:00Z',
    canDelete: true,
  },
];
let reference = {
  entryId: 17,
  g1: '现代汉语词典 — 学习',
  g2: '学而时习之',
  g3: '',
  g4: '',
  g5: '',
  originalRawReferenceMaterial: '',
  updatedAtUtc: '2026-07-20T05:30:00Z',
};
let history = [
  {
    id: 4,
    action: 'CommentAdded',
    performedByEditorId: 2,
    performedByDisplayName: 'ანა დამხმარე',
    performedAtUtc: '2026-07-20T05:00:00Z',
    oldValue: null,
    newValue: null,
    notes: null,
  },
  {
    id: 3,
    action: 'StatusChanged',
    performedByEditorId: 1,
    performedByDisplayName: 'ნინო რედაქტორი',
    performedAtUtc: '2026-07-19T12:00:00Z',
    oldValue: JSON.stringify({ status: 'NewEntry' }),
    newValue: JSON.stringify({ status: 'ZhReview' }),
    notes: null,
  },
  {
    id: 2,
    action: 'ContentEdited',
    performedByEditorId: 2,
    performedByDisplayName: 'ანა დამხმარე',
    performedAtUtc: '2026-07-18T09:00:00Z',
    oldValue: null,
    newValue: null,
    notes: null,
  },
  {
    id: 1,
    action: 'Created',
    performedByEditorId: 2,
    performedByDisplayName: 'ანა დამხმარე',
    performedAtUtc: '2026-07-18T08:00:00Z',
    oldValue: null,
    newValue: null,
    notes: null,
  },
];
let media = [
  {
    id: 1,
    entryId: 17,
    originalFilename: '学习-cover.png',
    mimeType: 'image/png',
    storageUrl: '/media/files/mock-image.png',
    thumbnailUrl: null,
    placement: 'cover',
    uploadedByEditorId: 1,
    uploadedByDisplayName: editor.displayName,
    uploadedAtUtc: '2026-07-20T06:00:00Z',
  },
];
let notifications = [
  {
    id: 1,
    entryId: 17,
    entryLemma: '学习',
    triggeredByEditorId: 2,
    triggeredByDisplayName: 'ანა დამხმარე',
    type: 'CommentAdded',
    isRead: false,
    createdAtUtc: '2026-07-20T05:00:00Z',
  },
];
const reviewFixture = process.env.QA_SCENARIO === 'photo-review';
if (reviewFixture) { entry = structuredClone(entryTemplate); comments[0].authorRole = 'Editor'; comments[0].commentText = 'გთხოვთ გადაამოწმოთ 学习 სტატია.'; reference.g1 = '都 dōu\n● all; both; entirely: 大家都来了。 Everyone has arrived.\n●● even; already: 都十二点了。 It is already twelve o’clock.'; }
// Default QA starts empty; opt in to a populated screenshot-review fixture.
if (!reviewFixture) { comments = []; supportMaterials = []; reference = null; history = []; media = []; notifications = []; }
let messages = [
  {
    id: 1,
    senderEditorId: 2,
    senderDisplayName: 'ანა დამხმარე',
    recipientEditorId: 1,
    recipientDisplayName: 'ნინო რედაქტორი',
    body: 'გთხოვ, გადაამოწმე სიტყვა 学习',
    sentAtUtc: '2026-07-20T05:40:00Z',
    readAtUtc: null,
  },
];
let editors = [
  editor,
  {
    id: 2,
    email: 'ana@chinese.ge',
    displayName: 'ანა დამხმარე',
    role: 'AssistantEditor',
    isActive: true,
    createdAtUtc: '2026-03-02T09:00:00Z',
  },
  {
    id: 3,
    email: 'giorgi@chinese.ge',
    displayName: 'გიორგი ქართულის რედაქტორი',
    role: 'KaEditor',
    isActive: true,
    createdAtUtc: '2026-04-11T09:00:00Z',
  },
];

const json = (response, status, body) => {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8' });
  response.end(body === undefined ? '' : JSON.stringify(body));
};
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);
let uploadedImage = png;
let nextSegmentId = 100;
const assignSegmentIds = (node) => {
  if (!node || typeof node !== 'object') return;
  if (Array.isArray(node)) { node.forEach(assignSegmentIds); return; }
  if ('definition' in node || 'zh' in node || 'gramGroups' in node || 'senses' in node) node.id ??= nextSegmentId++;
  Object.values(node).forEach(assignSegmentIds);
};
const image = (response) => {
  response.writeHead(200, { 'content-type': 'image/png', 'content-length': uploadedImage.length });
  response.end(uploadedImage);
};
const readBody = async (request) => {
  let raw = '';
  for await (const chunk of request) raw += chunk;
  return raw ? JSON.parse(raw) : {};
};
const summary = () => ({
  id: entry.id,
  lemma: entry.lemma,
  pinyin: entry.pinyin,
  status: entry.status,
  mainAuthorDisplayName: entry.mainAuthorDisplayName,
  lastEditorDisplayName: entry.lastEditorDisplayName,
  updatedAtUtc: entry.updatedAtUtc,
});

createServer(async (request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1');
  const path = url.pathname;
  if (request.method === 'OPTIONS') return json(response, 204);
  if (path === '/vocabularies') return json(response, 200, [
    { id: 1, category: 'pos', code: '名', label: '名 · Noun', sortOrder: 1 },
    { id: 2, category: 'pos', code: '动', label: '动 · Verb', sortOrder: 2 },
    { id: 3, category: 'style', code: '口', label: '口 · Colloquial', sortOrder: 1 },
    { id: 4, category: 'domain', code: '医', label: '医 · Medicine', sortOrder: 1 },
    { id: 5, category: 'abbr', code: 'ჩვეულ.', label: 'ჩვეულ.', sortOrder: 1 },
  ]);
  if (path === '/auth/login' || path === '/auth/refresh')
    return json(response, 200, {
      accessToken: 'qa-access-token',
      accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
      editor,
    });
  if (path === '/auth/logout') return json(response, 204);
  if ((path === '/entries' || path === '/entries/all') && request.method === 'GET') {
    const q = (url.searchParams.get('q') || '').trim().toLowerCase();
    const status = url.searchParams.get('status');
    const authorId = url.searchParams.get('authorId');
    const editorId = url.searchParams.get('editorId');
    const excludeDrafts = url.searchParams.get('excludeDrafts') === 'true' || path === '/entries';
    const matches = entry && (!excludeDrafts || entry.status !== 'NewEntry')
      && (!status || entry.status === status) && (!authorId || authorId === '1')
      && (!editorId || editorId === '1') && (!q || JSON.stringify(entry).toLowerCase().includes(q));
    const items = matches ? [summary()] : [];
    return json(response, 200, {
      items,
      totalCount: items.length,
      page: Number(url.searchParams.get('page')) || 1,
      pageSize: Number(url.searchParams.get('pageSize')) || 20,
    });
  }
  if (path === '/entries/search')
    return json(response, 200, entry && url.searchParams.get('q') ? [summary()] : []);
  if (path === '/entries' && request.method === 'POST') {
    const body = await readBody(request);
    const now = new Date().toISOString();
    entry = {
      ...entryTemplate,
      id: nextEntryId++,
      lemma: body.lemma,
      pinyin: null,
      pinyins: [],
      complexForms: [],
      status: 'NewEntry',
      homonyms: [],
      rowVersion: 1,
      mainAuthorDisplayName: editor.displayName,
      lastEditorDisplayName: editor.displayName,
      createdAtUtc: now,
      updatedAtUtc: now,
      capabilities: {
        canEditContent: true,
        allowedStatusTransitions: ['ZhReview', 'KaReview', 'Ready', 'Published', 'Archived'],
      },
    };
    comments = [];
    supportMaterials = [];
    reference = {
      entryId: entry.id,
      g1: '',
      g2: '',
      g3: '',
      g4: '',
      g5: '',
      originalRawReferenceMaterial: '',
      updatedAtUtc: now,
    };
    history = [
      {
        id: Date.now(),
        action: 'Created',
        performedByEditorId: editor.id,
        performedByDisplayName: editor.displayName,
        performedAtUtc: now,
        oldValue: null,
        newValue: null,
        notes: null,
      },
    ];
    media = [];
    return json(response, 201, entry);
  }
  if (entry && path === `/entries/${entry.id}` && request.method === 'GET')
    return json(response, 200, entry);
  if (entry && path === `/entries/${entry.id}/history` && request.method === 'GET')
    return json(response, 200, history);
  if (entry && path === `/entries/${entry.id}/media` && request.method === 'GET')
    return json(response, 200, media);
  if (entry && path === `/entries/${entry.id}/media` && request.method === 'POST') {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    const multipart = Buffer.concat(chunks).toString('utf8');
    const placement = multipart.match(/name="placement"\r\n\r\n([^\r\n]*)/)?.[1] ?? 'cover';
    const boundary = request.headers['content-type']?.match(/boundary=([^;]+)/)?.[1];
    if (boundary) {
      const filePart = Buffer.concat(chunks).toString('latin1').split('--' + boundary).find(part => /name="file";/.test(part));
      if (filePart) uploadedImage = Buffer.from(filePart.slice(filePart.indexOf('\r\n\r\n') + 4, -2), 'latin1');
    }
    const item = {
      id: Date.now(),
      entryId: entry.id,
      originalFilename: 'qa-upload.png',
      mimeType: 'image/png',
      storageUrl: '/media/files/mock-image.png',
      thumbnailUrl: null,
      placement,
      uploadedByEditorId: 1,
      uploadedByDisplayName: editor.displayName,
      uploadedAtUtc: new Date().toISOString(),
    };
    media = [item, ...media];
    history = [
      {
        id: Date.now(),
        action: 'ContentEdited',
        performedByEditorId: 1,
        performedByDisplayName: editor.displayName,
        performedAtUtc: new Date().toISOString(),
        oldValue: null,
        newValue: null,
        notes: `Image attached: ${item.originalFilename}`,
      },
      ...history,
    ];
    return json(response, 201, item);
  }
  if (entry && path === `/entries/${entry.id}/content` && request.method === 'PUT') {
    const body = await readBody(request);
    Object.values(body).forEach(assignSegmentIds);
    entry = {
      ...entry,
      ...body,
      rowVersion: entry.rowVersion + 1,
      updatedAtUtc: new Date().toISOString(),
    };
    history = [
      {
        id: Date.now(),
        action: 'ContentEdited',
        performedByEditorId: 1,
        performedByDisplayName: editor.displayName,
        performedAtUtc: new Date().toISOString(),
        oldValue: null,
        newValue: null,
        notes: null,
      },
      ...history,
    ];
    return json(response, 200, entry);
  }
  if (entry && path === `/entries/${entry.id}/status` && request.method === 'POST') {
    const body = await readBody(request);
    const previousStatus = entry.status;
    entry = {
      ...entry,
      status: body.targetStatus,
      updatedAtUtc: new Date().toISOString(),
      capabilities: {
        ...entry.capabilities,
        allowedStatusTransitions: [
          'NewEntry',
          'ZhReview',
          'KaReview',
          'Ready',
          'Published',
          'Archived',
        ].filter((status) => status !== body.targetStatus),
      },
    };
    history = [
      {
        id: Date.now(),
        action: 'StatusChanged',
        performedByEditorId: 1,
        performedByDisplayName: editor.displayName,
        performedAtUtc: new Date().toISOString(),
        oldValue: JSON.stringify({ status: previousStatus }),
        newValue: JSON.stringify({ status: body.targetStatus }),
        notes: null,
      },
      ...history,
    ];
    return json(response, 200, entry);
  }
  if (entry && path === `/entries/${entry.id}/comments` && request.method === 'GET')
    return json(response, 200, comments);
  if (entry && path === `/entries/${entry.id}/comments` && request.method === 'POST') {
    const body = await readBody(request);
    const item = {
      id: Date.now(),
      entryId: entry.id,
      authorEditorId: 1,
      authorDisplayName: editor.displayName,
      commentText: body.commentText,
      status: 'Active',
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };
    comments = [item, ...comments];
    history = [
      {
        id: Date.now(),
        action: 'CommentAdded',
        performedByEditorId: 1,
        performedByDisplayName: editor.displayName,
        performedAtUtc: new Date().toISOString(),
        oldValue: null,
        newValue: null,
        notes: null,
      },
      ...history,
    ];
    return json(response, 200, item);
  }
  if (/^\/comments\/\d+$/.test(path) && request.method === 'PUT') {
    const body = await readBody(request);
    const id = Number(path.split('/').pop());
    comments = comments.map((item) =>
      item.id === id
        ? { ...item, commentText: body.commentText, updatedAtUtc: new Date().toISOString() }
        : item,
    );
    return json(
      response,
      200,
      comments.find((item) => item.id === id),
    );
  }
  if (/^\/comments\/\d+$/.test(path) && request.method === 'DELETE') {
    const id = Number(path.split('/').pop());
    comments = comments.filter((item) => item.id !== id);
    return json(response, 204);
  }
  if (entry && path === `/entries/${entry.id}/reference-material` && request.method === 'GET')
    return json(response, 200, reference);
  if (entry && path === `/entries/${entry.id}/reference-material` && request.method === 'PUT') {
    reference = {
      ...reference,
      ...(await readBody(request)),
      updatedAtUtc: new Date().toISOString(),
    };
    history = [
      {
        id: Date.now(),
        action: 'ContentEdited',
        performedByEditorId: 1,
        performedByDisplayName: editor.displayName,
        performedAtUtc: new Date().toISOString(),
        oldValue: null,
        newValue: null,
        notes: 'Reference material updated.',
      },
      ...history,
    ];
    return json(response, 200, reference);
  }
  if (path === '/notifications' && request.method === 'GET')
    return json(
      response,
      200,
      url.searchParams.get('unreadOnly') === 'true'
        ? notifications.filter((item) => !item.isRead)
        : notifications,
    );
  if (/^\/notifications\/\d+\/read$/.test(path)) {
    const id = Number(path.split('/')[2]);
    notifications = notifications.map((item) =>
      item.id === id ? { ...item, isRead: true } : item,
    );
    return json(response, 204);
  }
  if (path === '/messages' && request.method === 'GET') return json(response, 200, messages);
  if (path === '/messages' && request.method === 'POST') {
    const body = await readBody(request);
    const recipient = editors.find((item) => item.id === body.recipientEditorId);
    const item = {
      id: Date.now(),
      senderEditorId: 1,
      senderDisplayName: editor.displayName,
      recipientEditorId: recipient.id,
      recipientDisplayName: recipient.displayName,
      body: body.body,
      sentAtUtc: new Date().toISOString(),
      readAtUtc: null,
    };
    messages = [...messages, item];
    return json(response, 200, item);
  }
  if (/^\/messages\/\d+\/read$/.test(path)) return json(response, 204);
  if (/^\/media\/files\/[a-zA-Z0-9._-]+$/.test(path) && request.method === 'GET')
    return image(response);
  if (/^\/media\/\d+$/.test(path) && request.method === 'DELETE') {
    const id = Number(path.split('/')[2]);
    media = media.filter((item) => item.id !== id);
    return json(response, 204);
  }
  if (path === '/editors/me' && request.method === 'PUT') {
    const body = await readBody(request);
    editor.displayName = body.displayName;
    return json(response, 200, editor);
  }
  if (path === '/editors/me/password' && request.method === 'POST') {
    const body = await readBody(request);
    if (body.currentPassword !== 'qa-password')
      return json(response, 400, { message: 'მიმდინარე პაროლი არასწორია.' });
    return json(response, 204);
  }
  if (path === '/editors' && request.method === 'GET') return json(response, 200, editors);
  if (path === '/editors' && request.method === 'POST') {
    const body = await readBody(request);
    const item = {
      id: Date.now(),
      email: body.email,
      displayName: body.displayName,
      role: body.role,
      isActive: true,
      createdAtUtc: new Date().toISOString(),
    };
    editors = [...editors, item];
    return json(response, 201, item);
  }
  if (entry && path === `/entries/${entry.id}/support-materials` && request.method === 'GET')
    return json(response, 200, supportMaterials);
  if (entry && path === `/entries/${entry.id}/support-materials` && request.method === 'POST') {
    const body = await readBody(request);
    const item = {
      id: Date.now(),
      entryId: entry.id,
      language: body.language,
      commentText: body.commentText,
      createdAtUtc: new Date().toISOString(),
      canDelete: true,
    };
    supportMaterials = [item, ...supportMaterials];
    return json(response, 201, item);
  }
  if (
    entry &&
    path.startsWith(`/entries/${entry.id}/support-materials/`) &&
    request.method === 'DELETE'
  ) {
    const id = Number(path.split('/').pop());
    supportMaterials = supportMaterials.filter((item) => item.id !== id);
    return json(response, 204);
  }
  if (/^\/editors\/\d+\/role$/.test(path)) {
    const id = Number(path.split('/')[2]);
    const body = await readBody(request);
    editors = editors.map((item) => (item.id === id ? { ...item, role: body.role } : item));
    return json(
      response,
      200,
      editors.find((item) => item.id === id),
    );
  }
  if (/^\/editors\/\d+\/(deactivate|reactivate)$/.test(path)) {
    const parts = path.split('/');
    const id = Number(parts[2]);
    editors = editors.map((item) =>
      item.id === id ? { ...item, isActive: parts[3] === 'reactivate' } : item,
    );
    return json(response, 204);
  }
  if (/^\/editors\/\d+\/reset-password$/.test(path)) return json(response, 204);
  if (path === '/accounting/entries-status-totals')
    return json(response, 200, {
      countsByStatus: {
        NewEntry: entry?.status === 'NewEntry' ? 1 : 0,
        ZhReview: entry?.status === 'ZhReview' ? 1 : 0,
        KaReview: entry?.status === 'KaReview' ? 1 : 0,
        Ready: entry?.status === 'Ready' ? 1 : 0,
        Published: entry?.status === 'Published' ? 1 : 0,
        Archived: entry?.status === 'Archived' ? 1 : 0,
      },
      total: entry ? 1 : 0,
    });
  if (path === '/accounting/editors')
    return json(response, 200, [
      {
        editorId: 1,
        displayName: editor.displayName,
        role: editor.role,
        mainScoreTotal: 24,
        additionalScoreTotal: 17,
        totalEntries: entry ? 1 : 0,
      },
      {
        editorId: 2,
        displayName: editors[1].displayName,
        role: editors[1].role,
        mainScoreTotal: 12,
        additionalScoreTotal: 9,
        totalEntries: 0,
      },
    ]);
  if (path === '/accounting/editors/1')
    return json(response, 200, {
      editorId: 1,
      displayName: editor.displayName,
      email: editor.email,
      role: editor.role,
      createdAtUtc: editor.createdAtUtc,
      scoresByStatus: {
        main: { Published: 24 },
        additional: { Ready: 5, Published: 12 },
      },
    });
  return json(response, 404, { message: `No mock route for ${request.method} ${path}` });
}).listen(5055, '127.0.0.1', () => console.log('QA mock API listening on http://127.0.0.1:5055'));
