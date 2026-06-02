import type { CurrentProvider, DocumentSummary, FormDownloadRef, ProviderStatus, SourceRef, UploadAgent } from './types';

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

export type ChatSourcesPayload = {
  sources: SourceRef[];
  formDownloads?: FormDownloadRef[];
};

export type UploadResult = {
  message: string;
  agent: string;
  chunks: number;
};

export type StreamChatHandlers = {
  onToken: (token: string) => void;
  onSources: (payload: ChatSourcesPayload) => void;
};

export async function streamChat(message: string, handlers: StreamChatHandlers, signal?: AbortSignal) {
  const response = await fetch(`${apiBaseUrl}/api/chat`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'text/event-stream',
    },
    body: JSON.stringify({ message }),
    signal,
  });

  if (!response.ok || !response.body) {
    throw new Error(await readError(response));
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { value, done } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    const events = buffer.split(/\n\n/);
    buffer = events.pop() ?? '';

    for (const event of events) {
      handleSseEvent(event, handlers);
    }
  }

  if (buffer.trim().length > 0) {
    handleSseEvent(buffer, handlers);
  }
}

export async function uploadDocument(file: File, agent: UploadAgent): Promise<UploadResult> {
  const formData = new FormData();
  formData.append('file', file);

  const query = agent ? `?agent=${encodeURIComponent(agent)}` : '';
  const response = await fetch(`${apiBaseUrl}/api/ingest${query}`, {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return response.json() as Promise<UploadResult>;
}

export async function fetchDocuments(): Promise<DocumentSummary[]> {
  const response = await fetch(`${apiBaseUrl}/api/documents`);

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return response.json() as Promise<DocumentSummary[]>;
}

export async function deleteDocument(sourceFile: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(sourceFile)}`, {
    method: 'DELETE',
  });

  if (!response.ok) {
    throw new Error(await readError(response));
  }
}

export async function fetchCurrentProvider(): Promise<CurrentProvider> {
  return fetchJson<CurrentProvider>('/api/providers/current');
}

export async function fetchProviderStatus(): Promise<ProviderStatus> {
  return fetchJson<ProviderStatus>('/api/providers/status');
}

export function toAssetUrl(path: string) {
  if (!path) {
    return '';
  }

  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${apiBaseUrl}${path.startsWith('/') ? path : `/${path}`}`;
}

function handleSseEvent(event: string, handlers: StreamChatHandlers) {
  const data = event
    .split(/\n/)
    .filter((line) => line.startsWith('data:'))
    .map((line) => {
      const value = line.slice(5);
      return value.startsWith(' ') ? value.slice(1) : value;
    })
    .join('\n');

  if (!data) {
    return;
  }

  if (data.startsWith('[SOURCES]')) {
    const rawJson = data.slice('[SOURCES]'.length);
    handlers.onSources(JSON.parse(rawJson) as ChatSourcesPayload);
    return;
  }

  handlers.onToken(data);
}

async function readError(response: Response) {
  const text = await response.text();
  if (!text) {
    return `Request failed with status ${response.status}`;
  }

  try {
    const parsed = JSON.parse(text) as { error?: string; title?: string };
    return parsed.error ?? parsed.title ?? text;
  } catch {
    return text;
  }
}

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`);

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return response.json() as Promise<T>;
}
