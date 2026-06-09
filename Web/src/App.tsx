import {
  AlertCircle,
  Bot,
  Check,
  ChevronDown,
  ChevronRight,
  FileText,
  FolderOpen,
  Loader2,
  MessageSquareText,
  RefreshCw,
  Send,
  Upload,
  X,
} from 'lucide-react';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import {
  acceptSuggestion,
  chooseSuggestionTemplate,
  fetchSuggestions,
  rejectSuggestion,
  streamChat,
  uploadDocument,
} from './api';
import type {
  AgentValue,
  ChatMessage,
  FormDownloadRef,
  FormMappingSuggestion,
  IngestResponse,
  SourceRef,
} from './types';

type Page = 'chat' | 'documents';

const agentOptions: Array<{ label: string; value: AgentValue }> = [
  { label: 'Auto-detect', value: '' },
  { label: 'ELCA HR', value: 'ELCA_HR' },
  { label: 'ELCA General', value: 'ELCA_GENERAL' },
  { label: 'CII Tower Support', value: 'CII_TOWER_SUPPORT' },
];

const starterMessages: ChatMessage[] = [
  {
    id: crypto.randomUUID(),
    role: 'assistant',
    content: 'Ask about policies, procedures, facilities, forms, or HR guidance. I will answer from indexed documents and cite the source.',
  },
];

export default function App() {
  const [page, setPage] = useState<Page>('chat');
  const [suggestions, setSuggestions] = useState<FormMappingSuggestion[]>([]);
  const [suggestionsLoading, setSuggestionsLoading] = useState(false);
  const [suggestionsError, setSuggestionsError] = useState('');

  const loadSuggestions = async () => {
    setSuggestionsLoading(true);
    setSuggestionsError('');
    try {
      setSuggestions(await fetchSuggestions());
    } catch (error) {
      setSuggestionsError(error instanceof Error ? error.message : 'Could not load suggestions.');
    } finally {
      setSuggestionsLoading(false);
    }
  };

  useEffect(() => {
    void loadSuggestions();
  }, []);

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <div className="brand-mark">
            <Bot size={20} aria-hidden="true" />
          </div>
          <div>
            <p className="brand-title">Policy Bot</p>
            <p className="brand-subtitle">RAG workspace</p>
          </div>
        </div>

        <nav className="nav-list" aria-label="Primary">
          <button className={page === 'chat' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('chat')}>
            <MessageSquareText size={18} aria-hidden="true" />
            <span>Chat</span>
          </button>
          <button className={page === 'documents' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('documents')}>
            <FolderOpen size={18} aria-hidden="true" />
            <span>Documents</span>
          </button>
        </nav>

        <div className="sidebar-status">
          <span className="status-dot" />
          Backend via Vite proxy
        </div>
      </aside>

      <main className="main-surface">
        {page === 'chat' ? (
          <ChatPage />
        ) : (
          <DocumentsPage
            suggestions={suggestions}
            setSuggestions={setSuggestions}
            reloadSuggestions={loadSuggestions}
            suggestionsLoading={suggestionsLoading}
            suggestionsError={suggestionsError}
          />
        )}
      </main>
    </div>
  );
}

function ChatPage() {
  const [messages, setMessages] = useState<ChatMessage[]>(starterMessages);
  const [input, setInput] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [waitingForFirstToken, setWaitingForFirstToken] = useState(false);
  const [error, setError] = useState('');
  const abortRef = useRef<AbortController | null>(null);
  const endRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages, waitingForFirstToken]);

  const sendMessage = async (event: FormEvent) => {
    event.preventDefault();
    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    const assistantId = crypto.randomUUID();
    abortRef.current?.abort();
    abortRef.current = new AbortController();
    setInput('');
    setError('');
    setIsStreaming(true);
    setWaitingForFirstToken(true);
    setMessages((current) => [
      ...current,
      { id: crypto.randomUUID(), role: 'user', content: text },
      { id: assistantId, role: 'assistant', content: '' },
    ]);

    try {
      await streamChat(
        text,
        {
          onToken: (token) => {
            setWaitingForFirstToken(false);
            setMessages((current) =>
              current.map((message) =>
                message.id === assistantId
                  ? { ...message, content: message.content + token }
                  : message,
              ),
            );
          },
          onSources: (payload) => {
            setMessages((current) =>
              current.map((message) =>
                message.id === assistantId
                  ? {
                      ...message,
                      sources: payload.sources,
                      formDownloads: payload.formDownloads ?? [],
                    }
                  : message,
              ),
            );
          },
        },
        abortRef.current.signal,
      );
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setError(error instanceof Error ? error.message : 'Chat request failed.');
        setMessages((current) =>
          current.map((message) =>
            message.id === assistantId && !message.content
              ? { ...message, content: 'I could not complete that request.' }
              : message,
          ),
        );
      }
    } finally {
      setIsStreaming(false);
      setWaitingForFirstToken(false);
    }
  };

  const stopStreaming = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
    setWaitingForFirstToken(false);
  };

  return (
    <section className="page-grid chat-layout">
      <div className="page-header">
        <div>
          <p className="eyebrow">Policy assistant</p>
          <h1>Chat</h1>
        </div>
        <div className="header-meta">
          <span className="status-dot" />
          Streaming answers
        </div>
      </div>

      <div className="chat-panel">
        <div className="message-list" aria-live="polite">
          {messages.map((message) => (
            <MessageBubble key={message.id} message={message} />
          ))}

          {waitingForFirstToken && (
            <div className="message-row assistant">
              <div className="avatar assistant-avatar">
                <Loader2 size={16} className="spin" aria-hidden="true" />
              </div>
              <div className="message-bubble assistant-bubble muted-bubble">Searching policy context...</div>
            </div>
          )}

          <div ref={endRef} />
        </div>

        {error && <ErrorBanner message={error} />}

        <form className="composer" onSubmit={sendMessage}>
          <textarea
            value={input}
            onChange={(event) => setInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void sendMessage(event);
              }
            }}
            placeholder="Ask about annual leave, payment requests, CII Tower support..."
            rows={2}
          />
          {isStreaming ? (
            <button type="button" className="icon-button stop" onClick={stopStreaming} aria-label="Stop response">
              <X size={18} aria-hidden="true" />
            </button>
          ) : (
            <button type="submit" className="send-button" disabled={!input.trim()} aria-label="Send message">
              <Send size={18} aria-hidden="true" />
            </button>
          )}
        </form>
      </div>
    </section>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isAssistant = message.role === 'assistant';

  return (
    <article className={isAssistant ? 'message-row assistant' : 'message-row user'}>
      {isAssistant && (
        <div className="avatar assistant-avatar">
          <Bot size={16} aria-hidden="true" />
        </div>
      )}
      <div className={isAssistant ? 'message-stack assistant-stack' : 'message-stack user-stack'}>
        <div className={isAssistant ? 'message-bubble assistant-bubble' : 'message-bubble user-bubble'}>
          {message.content || ' '}
        </div>
        {isAssistant && (
          <SourcesPanel sources={message.sources ?? []} formDownloads={message.formDownloads ?? []} />
        )}
      </div>
    </article>
  );
}

function SourcesPanel({
  sources,
  formDownloads,
}: {
  sources: SourceRef[];
  formDownloads: FormDownloadRef[];
}) {
  if (sources.length === 0 && formDownloads.length === 0) {
    return null;
  }

  return (
    <div className="sources-panel">
      {sources.length > 0 && (
        <div className="source-list">
          {sources.map((source, index) => {
            const imagePaths = source.imagePaths?.length ? source.imagePaths : source.imagePath ? [source.imagePath] : [];
            return (
              <div className="source-item" key={`${source.file}-${source.page}-${index}`}>
                <div className="source-chip">
                  <FileText size={14} aria-hidden="true" />
                  <span>{source.file} - Page {source.page}</span>
                </div>
                {source.formDownload && <DownloadLink download={source.formDownload} />}
                {imagePaths.length > 0 && (
                  <div className="source-images">
                    {imagePaths.map((path) => (
                      <img key={path} src={path} alt={`${source.file}, page ${source.page}`} loading="lazy" />
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {formDownloads.length > 0 && (
        <div className="download-list">
          {formDownloads.map((download) => (
            <DownloadLink key={`${download.formName}-${download.downloadPath}`} download={download} />
          ))}
        </div>
      )}
    </div>
  );
}

function DownloadLink({ download }: { download: FormDownloadRef }) {
  return (
    <a className="download-link" href={download.downloadPath}>
      <FileText size={14} aria-hidden="true" />
      {download.formName}
    </a>
  );
}

function DocumentsPage({
  suggestions,
  setSuggestions,
  reloadSuggestions,
  suggestionsLoading,
  suggestionsError,
}: {
  suggestions: FormMappingSuggestion[];
  setSuggestions: (suggestions: FormMappingSuggestion[]) => void;
  reloadSuggestions: () => Promise<void>;
  suggestionsLoading: boolean;
  suggestionsError: string;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [agent, setAgent] = useState<AgentValue>('');
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [uploadResult, setUploadResult] = useState<IngestResponse | null>(null);

  const handleUpload = async (event: FormEvent) => {
    event.preventDefault();
    if (!file || uploading) {
      return;
    }

    setUploading(true);
    setUploadError('');
    setUploadResult(null);
    try {
      const result = await uploadDocument(file, agent);
      setUploadResult(result);
      if (result.formMappingSuggestion) {
        setSuggestions([result.formMappingSuggestion, ...suggestions.filter((item) => item.id !== result.formMappingSuggestion?.id)]);
      } else {
        await reloadSuggestions();
      }
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  };

  const upsertSuggestion = (updated: FormMappingSuggestion) => {
    setSuggestions(suggestions.map((suggestion) => (suggestion.id === updated.id ? updated : suggestion)));
  };

  return (
    <section className="page-grid documents-layout">
      <div className="page-header">
        <div>
          <p className="eyebrow">Knowledge base</p>
          <h1>Documents</h1>
        </div>
        <button className="secondary-button" onClick={() => void reloadSuggestions()} disabled={suggestionsLoading}>
          <RefreshCw size={16} className={suggestionsLoading ? 'spin' : ''} aria-hidden="true" />
          Refresh
        </button>
      </div>

      <div className="documents-grid">
        <section className="panel upload-panel">
          <div className="panel-heading">
            <Upload size={18} aria-hidden="true" />
            <h2>Upload document</h2>
          </div>

          <form className="upload-form" onSubmit={handleUpload}>
            <label className="field-label" htmlFor="document-file">File</label>
            <input
              id="document-file"
              type="file"
              accept=".pdf,.docx,.doc,.xlsx,.pptx"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            />

            <label className="field-label" htmlFor="agent-select">Agent</label>
            <div className="select-wrap">
              <select id="agent-select" value={agent} onChange={(event) => setAgent(event.target.value as AgentValue)}>
                {agentOptions.map((option) => (
                  <option key={option.value || 'auto'} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <ChevronDown size={16} aria-hidden="true" />
            </div>

            <button className="primary-button" type="submit" disabled={!file || uploading}>
              {uploading ? <Loader2 size={16} className="spin" aria-hidden="true" /> : <Upload size={16} aria-hidden="true" />}
              Upload
            </button>
          </form>

          {uploadError && <ErrorBanner message={uploadError} />}
          {uploadResult && (
            <div className="success-box">
              <Check size={16} aria-hidden="true" />
              <div>
                <strong>{uploadResult.message ?? 'Document ingested successfully.'}</strong>
                <span>{uploadResult.agent ?? 'Auto-detected'} - {uploadResult.chunks ?? 0} chunks</span>
              </div>
            </div>
          )}

          {uploadResult?.formMappingSuggestion && (
            <SuggestionCard suggestion={uploadResult.formMappingSuggestion} onUpdate={upsertSuggestion} compact />
          )}
        </section>

        <section className="panel review-panel">
          <div className="panel-heading">
            <FileText size={18} aria-hidden="true" />
            <h2>Form mapping review</h2>
          </div>
          <p className="panel-copy">This is a suggested mapping. Review before accepting.</p>

          {suggestionsError && <ErrorBanner message={suggestionsError} />}
          {suggestionsLoading && suggestions.length === 0 ? (
            <div className="empty-state">
              <Loader2 size={18} className="spin" aria-hidden="true" />
              Loading suggestions
            </div>
          ) : (
            <SuggestionGroups suggestions={suggestions} onUpdate={upsertSuggestion} />
          )}
        </section>
      </div>
    </section>
  );
}

function SuggestionGroups({
  suggestions,
  onUpdate,
}: {
  suggestions: FormMappingSuggestion[];
  onUpdate: (suggestion: FormMappingSuggestion) => void;
}) {
  const groups = useMemo(
    () => [
      { title: 'Recommended', statuses: ['pending_review'] },
      { title: 'Needs review', statuses: ['needs_manual_review'] },
      { title: 'Accepted', statuses: ['accepted'] },
      { title: 'Rejected', statuses: ['rejected'] },
    ],
    [],
  );

  if (suggestions.length === 0) {
    return <div className="empty-state">No mapping suggestions yet.</div>;
  }

  return (
    <div className="suggestion-groups">
      {groups.map((group) => {
        const items = suggestions.filter((suggestion) => group.statuses.includes(suggestion.status));
        return (
          <details className="suggestion-group" key={group.title} open={items.length > 0}>
            <summary>
              <span>
                <ChevronRight size={15} aria-hidden="true" />
                {group.title}
              </span>
              <strong>{items.length}</strong>
            </summary>
            <div className="suggestion-list">
              {items.length === 0 ? (
                <div className="empty-state small">No items</div>
              ) : (
                items.map((suggestion) => (
                  <SuggestionCard key={suggestion.id} suggestion={suggestion} onUpdate={onUpdate} />
                ))
              )}
            </div>
          </details>
        );
      })}
    </div>
  );
}

function SuggestionCard({
  suggestion,
  onUpdate,
  compact = false,
}: {
  suggestion: FormMappingSuggestion;
  onUpdate: (suggestion: FormMappingSuggestion) => void;
  compact?: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [templateFile, setTemplateFile] = useState(suggestion.candidateTemplateFile);
  const [downloadPath, setDownloadPath] = useState(suggestion.candidateDownloadPath);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const canReview = suggestion.status === 'pending_review' || suggestion.status === 'needs_manual_review';

  const runAction = async (action: () => Promise<FormMappingSuggestion>) => {
    setBusy(true);
    setError('');
    try {
      onUpdate(await action());
      setEditing(false);
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Suggestion update failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <article className={compact ? 'suggestion-card compact' : 'suggestion-card'}>
      <div className="suggestion-topline">
        <div>
          <h3>{suggestion.formName || 'Unnamed form'}</h3>
          <p>{suggestion.agent || 'Unassigned'} - {Math.round(suggestion.confidence * 100)}% confidence</p>
        </div>
        <span className={`status-pill ${suggestion.status}`}>{formatStatus(suggestion.status)}</span>
      </div>

      <dl className="suggestion-meta">
        <div>
          <dt>Suggested template</dt>
          <dd>{suggestion.candidateTemplateFile || 'None'}</dd>
        </div>
        <div>
          <dt>Download path</dt>
          <dd>{suggestion.candidateDownloadPath || 'None'}</dd>
        </div>
        <div>
          <dt>Reason</dt>
          <dd>{suggestion.reason || 'No reason provided.'}</dd>
        </div>
      </dl>

      {editing && (
        <div className="choose-template">
          <label className="field-label" htmlFor={`template-${suggestion.id}`}>Candidate template file</label>
          <input id={`template-${suggestion.id}`} value={templateFile} onChange={(event) => setTemplateFile(event.target.value)} />
          <label className="field-label" htmlFor={`path-${suggestion.id}`}>Candidate download path</label>
          <input id={`path-${suggestion.id}`} value={downloadPath} onChange={(event) => setDownloadPath(event.target.value)} />
        </div>
      )}

      {error && <ErrorBanner message={error} />}

      {canReview && (
        <div className="suggestion-actions">
          <button className="secondary-button" disabled={busy} onClick={() => void runAction(() => acceptSuggestion(suggestion.id))}>
            <Check size={15} aria-hidden="true" />
            Accept
          </button>
          <button className="secondary-button danger" disabled={busy} onClick={() => void runAction(() => rejectSuggestion(suggestion.id))}>
            <X size={15} aria-hidden="true" />
            Reject
          </button>
          {editing ? (
            <button
              className="secondary-button"
              disabled={busy || !templateFile.trim() || !downloadPath.trim()}
              onClick={() => void runAction(() => chooseSuggestionTemplate(suggestion.id, templateFile, downloadPath))}
            >
              Save choice
            </button>
          ) : (
            <button className="secondary-button" disabled={busy} onClick={() => setEditing(true)}>
              Choose another
            </button>
          )}
        </div>
      )}
    </article>
  );
}

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="error-banner">
      <AlertCircle size={16} aria-hidden="true" />
      {message}
    </div>
  );
}

function formatStatus(status: FormMappingSuggestion['status']) {
  return status.replaceAll('_', ' ');
}
