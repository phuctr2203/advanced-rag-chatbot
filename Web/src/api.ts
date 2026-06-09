import type {
  AgentValue,
  CurrentProviderResponse,
  DocumentSummary,
  FormMappingSuggestion,
  IngestResponse,
  ProviderStatusResponse,
  SourcesPayload,
} from './types';

type StreamHandlers = {
  onToken: (token: string) => void;
  onSources: (payload: SourcesPayload) => void;
};

export async function streamChat(message: string, handlers: StreamHandlers, signal?: AbortSignal) {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: {
      Accept: 'text/event-stream',
      'Content-Type': 'application/json',
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
    buffer += decoder.decode(value ?? new Uint8Array(), { stream: !done });

    let separator = findSseSeparator(buffer);
    while (separator) {
      const rawEvent = buffer.slice(0, separator.index);
      buffer = buffer.slice(separator.index + separator.length);
      handleSseEvent(rawEvent, handlers);
      separator = findSseSeparator(buffer);
    }

    if (done) {
      if (buffer.trim()) {
        handleSseEvent(buffer, handlers);
      }
      break;
    }
  }
}

export async function uploadDocument(file: File, agent: AgentValue) {
  const form = new FormData();
  form.append('file', file);

  const query = agent ? `?agent=${encodeURIComponent(agent)}` : '';
  const response = await fetch(`/api/ingest${query}`, {
    method: 'POST',
    body: form,
  });
  const payload = (await response.json()) as IngestResponse;

  if (!response.ok) {
    throw new Error(payload.error ?? 'Document upload failed.');
  }

  return normalizeIngestResponse(payload);
}

export async function fetchSuggestions() {
  const response = await fetch('/api/form-registry/suggestions');
  if (!response.ok) {
    throw new Error(await readError(response));
  }

  const data = (await response.json()) as unknown[];
  return data.map(normalizeSuggestion);
}

export async function fetchDocuments() {
  const response = await fetch('/api/documents');
  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return (await response.json()) as DocumentSummary[];
}

export async function deleteDocument(sourceFile: string) {
  const response = await fetch(`/api/documents/${encodeURIComponent(sourceFile)}`, {
    method: 'DELETE',
  });
  const payload = await response.json();

  if (!response.ok) {
    throw new Error(payload.error ?? 'Document deletion failed.');
  }

  return payload as { sourceFile: string; deletedChunks: number };
}

export async function fetchCurrentProvider() {
  const response = await fetch('/api/providers/current');
  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return (await response.json()) as CurrentProviderResponse;
}

export async function fetchProviderStatus() {
  const response = await fetch('/api/providers/status');
  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return (await response.json()) as ProviderStatusResponse;
}

export async function acceptSuggestion(id: string) {
  return updateSuggestion(`/api/form-registry/suggestions/${encodeURIComponent(id)}/accept`);
}

export async function rejectSuggestion(id: string) {
  return updateSuggestion(`/api/form-registry/suggestions/${encodeURIComponent(id)}/reject`);
}

export async function chooseSuggestionTemplate(
  id: string,
  candidateTemplateFile: string,
  candidateDownloadPath: string,
) {
  return updateSuggestion(`/api/form-registry/suggestions/${encodeURIComponent(id)}/choose-template`, {
    candidateTemplateFile,
    candidateDownloadPath,
  });
}

async function updateSuggestion(url: string, body?: Record<string, string>) {
  const response = await fetch(url, {
    method: 'POST',
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const payload = await response.json();

  if (!response.ok) {
    throw new Error(payload.error ?? 'Suggestion update failed.');
  }

  return normalizeSuggestion(payload);
}

function findSseSeparator(buffer: string) {
  const candidates = ['\r\n\r\n', '\n\n', '\r\r']
    .map((value) => ({ index: buffer.indexOf(value), length: value.length }))
    .filter((value) => value.index >= 0)
    .sort((left, right) => left.index - right.index);

  return candidates[0] ?? null;
}

function handleSseEvent(rawEvent: string, handlers: StreamHandlers) {
  const data = rawEvent
    .split(/\r\n|\n|\r/)
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(5).replace(/^ /, ''))
    .join('\n');

  if (!data) {
    return;
  }

  if (data.startsWith('[SOURCES]')) {
    handlers.onSources(JSON.parse(data.slice('[SOURCES]'.length)) as SourcesPayload);
    return;
  }

  handlers.onToken(data);
}

async function readError(response: Response) {
  const text = await response.text();
  if (!text) {
    return `Request failed with status ${response.status}.`;
  }

  try {
    const payload = JSON.parse(text) as { error?: string };
    return payload.error ?? text;
  } catch {
    return text;
  }
}

function normalizeIngestResponse(payload: IngestResponse) {
  return {
    ...payload,
    formMappingSuggestion: payload.formMappingSuggestion
      ? normalizeSuggestion(payload.formMappingSuggestion)
      : null,
  };
}

function normalizeSuggestion(value: unknown): FormMappingSuggestion {
  const source = value as Record<string, unknown>;
  return {
    id: String(source.id ?? ''),
    formName: String(source.formName ?? source.form_name ?? ''),
    aliases: Array.isArray(source.aliases) ? source.aliases.map(String) : [],
    candidateTemplateFile: String(source.candidateTemplateFile ?? source.candidate_template_file ?? ''),
    candidateDownloadPath: String(source.candidateDownloadPath ?? source.candidate_download_path ?? ''),
    agent: String(source.agent ?? ''),
    confidence: Number(source.confidence ?? 0),
    reason: String(source.reason ?? ''),
    status: String(source.status ?? 'needs_manual_review') as FormMappingSuggestion['status'],
  };
}
