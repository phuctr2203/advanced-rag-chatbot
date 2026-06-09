import {
  AlertCircle,
  Activity,
  Bot,
  Building2,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronRight,
  CreditCard,
  Database,
  FileText,
  FolderOpen,
  Loader2,
  MessageSquareText,
  RefreshCw,
  Send,
  Trash2,
  Upload,
  X,
} from 'lucide-react';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import {
  acceptSuggestion,
  chooseSuggestionTemplate,
  deleteDocument,
  fetchCurrentProvider,
  fetchDocuments,
  fetchProviderStatus,
  fetchSuggestions,
  rejectSuggestion,
  streamChat,
  uploadDocument,
} from './api';
import type {
  AgentValue,
  ChatMessage,
  CurrentProviderResponse,
  DocumentSummary,
  FormDownloadRef,
  FormMappingSuggestion,
  IngestResponse,
  ProviderStatusResponse,
  ServiceStatus,
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

const chatPromptSuggestions = [
  {
    label: 'Payment',
    question: 'How do I submit a payment request and where can I download the form?',
    icon: CreditCard,
  },
  {
    label: 'Annual leave',
    question: 'How many additional annual leave days do I get based on seniority?',
    icon: CalendarDays,
  },
  {
    label: 'CII Tower',
    question: 'What should I know about CII Tower support and building rules?',
    icon: Building2,
  },
];

export default function App() {
  const [page, setPage] = useState<Page>('chat');
  const [suggestions, setSuggestions] = useState<FormMappingSuggestion[]>([]);
  const [suggestionsLoading, setSuggestionsLoading] = useState(false);
  const [suggestionsError, setSuggestionsError] = useState('');
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState('');
  const [provider, setProvider] = useState<CurrentProviderResponse | null>(null);
  const [providerStatus, setProviderStatus] = useState<ProviderStatusResponse | null>(null);
  const [providerError, setProviderError] = useState('');

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

  const loadDocuments = async () => {
    setDocumentsLoading(true);
    setDocumentsError('');
    try {
      setDocuments(await fetchDocuments());
    } catch (error) {
      setDocumentsError(error instanceof Error ? error.message : 'Could not load documents.');
    } finally {
      setDocumentsLoading(false);
    }
  };

  const loadProviderInfo = async () => {
    setProviderError('');
    try {
      const [current, status] = await Promise.all([fetchCurrentProvider(), fetchProviderStatus()]);
      setProvider(current);
      setProviderStatus(status);
    } catch (error) {
      setProviderError(error instanceof Error ? error.message : 'Could not load provider status.');
    }
  };

  useEffect(() => {
    void loadSuggestions();
    void loadDocuments();
    void loadProviderInfo();
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
            documents={documents}
            setDocuments={setDocuments}
            reloadDocuments={loadDocuments}
            documentsLoading={documentsLoading}
            documentsError={documentsError}
            provider={provider}
            providerStatus={providerStatus}
            providerError={providerError}
            reloadProviderInfo={loadProviderInfo}
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

  const submitMessage = async (text: string) => {
    const trimmedText = text.trim();
    if (!trimmedText || isStreaming) {
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
      { id: crypto.randomUUID(), role: 'user', content: trimmedText },
      { id: assistantId, role: 'assistant', content: '' },
    ]);

    try {
      await streamChat(
        trimmedText,
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

  const sendMessage = async (event: FormEvent) => {
    event.preventDefault();
    await submitMessage(input);
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

      <div className="prompt-suggestions" aria-label="Suggested questions">
        {chatPromptSuggestions.map(({ label, question, icon: Icon }) => (
          <button
            key={label}
            type="button"
            className="prompt-suggestion"
            onClick={() => void submitMessage(question)}
            disabled={isStreaming}
          >
            <Icon size={17} aria-hidden="true" />
            <span>
              <strong>{label}</strong>
              <small>{question}</small>
            </span>
          </button>
        ))}
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
  documents,
  setDocuments,
  reloadDocuments,
  documentsLoading,
  documentsError,
  provider,
  providerStatus,
  providerError,
  reloadProviderInfo,
}: {
  suggestions: FormMappingSuggestion[];
  setSuggestions: (suggestions: FormMappingSuggestion[]) => void;
  reloadSuggestions: () => Promise<void>;
  suggestionsLoading: boolean;
  suggestionsError: string;
  documents: DocumentSummary[];
  setDocuments: (documents: DocumentSummary[]) => void;
  reloadDocuments: () => Promise<void>;
  documentsLoading: boolean;
  documentsError: string;
  provider: CurrentProviderResponse | null;
  providerStatus: ProviderStatusResponse | null;
  providerError: string;
  reloadProviderInfo: () => Promise<void>;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [agent, setAgent] = useState<AgentValue>('');
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [uploadResult, setUploadResult] = useState<IngestResponse | null>(null);
  const [deletingDocument, setDeletingDocument] = useState('');

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
      await reloadDocuments();
      await reloadProviderInfo();
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  };

  const upsertSuggestion = (updated: FormMappingSuggestion) => {
    setSuggestions(suggestions.map((suggestion) => (suggestion.id === updated.id ? updated : suggestion)));
  };

  const handleDeleteDocument = async (sourceFile: string) => {
    if (!window.confirm(`Remove all indexed chunks for "${sourceFile}"?`)) {
      return;
    }

    setDeletingDocument(sourceFile);
    setUploadError('');
    try {
      await deleteDocument(sourceFile);
      setDocuments(documents.filter((document) => document.sourceFile !== sourceFile));
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Document deletion failed.');
    } finally {
      setDeletingDocument('');
    }
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

        <section className="panel system-panel">
          <div className="panel-heading">
            <Activity size={18} aria-hidden="true" />
            <h2>System status</h2>
          </div>
          {providerError && <ErrorBanner message={providerError} />}
          <ProviderSummary provider={provider} status={providerStatus} onRefresh={reloadProviderInfo} />
        </section>

        <section className="panel library-panel">
          <div className="panel-heading split-heading">
            <div>
              <Database size={18} aria-hidden="true" />
              <h2>Indexed documents</h2>
            </div>
            <button className="secondary-button" onClick={() => void reloadDocuments()} disabled={documentsLoading}>
              <RefreshCw size={16} className={documentsLoading ? 'spin' : ''} aria-hidden="true" />
              Refresh
            </button>
          </div>
          {documentsError && <ErrorBanner message={documentsError} />}
          <DocumentLibrary
            documents={documents}
            loading={documentsLoading}
            deletingDocument={deletingDocument}
            onDelete={handleDeleteDocument}
          />
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

function ProviderSummary({
  provider,
  status,
  onRefresh,
}: {
  provider: CurrentProviderResponse | null;
  status: ProviderStatusResponse | null;
  onRefresh: () => Promise<void>;
}) {
  const statuses = status ? [status.llm, status.embedding, status.qdrant] : [];

  return (
    <div className="provider-summary">
      <div className="provider-grid">
        <div>
          <span>LLM</span>
          <strong>{provider ? `${provider.llmProvider} / ${provider.llmModel || 'No model'}` : 'Loading'}</strong>
        </div>
        <div>
          <span>Vision</span>
          <strong>{provider ? `${provider.visionProvider} / ${provider.visionModel || 'No model'}` : 'Loading'}</strong>
        </div>
        <div>
          <span>Embedding</span>
          <strong>{provider ? `${provider.embeddingProvider} / ${provider.embeddingBaseUrl}` : 'Loading'}</strong>
        </div>
      </div>

      <div className="status-chip-row">
        {statuses.length === 0 ? (
          <span className="status-chip pending">Checking services</span>
        ) : (
          statuses.map((item) => <StatusChip key={item.name} status={item} />)
        )}
      </div>

      <button className="secondary-button compact-button" onClick={() => void onRefresh()}>
        <RefreshCw size={15} aria-hidden="true" />
        Refresh status
      </button>
    </div>
  );
}

function StatusChip({ status }: { status: ServiceStatus }) {
  return (
    <span className={status.healthy ? 'status-chip healthy' : 'status-chip unhealthy'} title={status.message}>
      <span className="status-dot" />
      {status.name}
    </span>
  );
}

function DocumentLibrary({
  documents,
  loading,
  deletingDocument,
  onDelete,
}: {
  documents: DocumentSummary[];
  loading: boolean;
  deletingDocument: string;
  onDelete: (sourceFile: string) => Promise<void>;
}) {
  if (loading && documents.length === 0) {
    return (
      <div className="empty-state">
        <Loader2 size={18} className="spin" aria-hidden="true" />
        Loading documents
      </div>
    );
  }

  if (documents.length === 0) {
    return <div className="empty-state">No indexed documents yet.</div>;
  }

  return (
    <div className="document-table-wrap">
      <table className="document-table">
        <thead>
          <tr>
            <th>Filename</th>
            <th>Agent</th>
            <th>Type</th>
            <th>Chunks</th>
            <th>Pages</th>
            <th>Images</th>
            <th>Template</th>
            <th aria-label="Actions" />
          </tr>
        </thead>
        <tbody>
          {documents.map((document) => (
            <tr key={document.sourceFile}>
              <td>
                <div className="document-name">
                  <FileText size={15} aria-hidden="true" />
                  <span title={document.sourceFile}>{document.sourceFile}</span>
                </div>
              </td>
              <td>{document.agent || 'Unknown'}</td>
              <td>{document.fileType || 'Unknown'}</td>
              <td>{document.chunkCount}</td>
              <td>{document.pageCount}</td>
              <td>{document.hasImages ? 'Yes' : 'No'}</td>
              <td>{document.hasFormTemplate ? 'Yes' : 'No'}</td>
              <td>
                <button
                  className="icon-button danger"
                  onClick={() => void onDelete(document.sourceFile)}
                  disabled={deletingDocument === document.sourceFile}
                  aria-label={`Delete ${document.sourceFile}`}
                  title="Delete indexed document"
                >
                  {deletingDocument === document.sourceFile ? (
                    <Loader2 size={16} className="spin" aria-hidden="true" />
                  ) : (
                    <Trash2 size={16} aria-hidden="true" />
                  )}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
