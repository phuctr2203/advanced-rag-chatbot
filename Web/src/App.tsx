import {
  AlertCircle,
  Bot,
  Check,
  ChevronRight,
  Download,
  FileText,
  FolderOpen,
  Loader2,
  MessageSquareText,
  RefreshCw,
  Search,
  Send,
  Star,
  Trash2,
  Upload,
  X,
} from 'lucide-react';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import {
  acceptSuggestion,
  chooseSuggestionTemplate,
  deleteDocument,
  fetchDocuments,
  fetchSuggestions,
  rejectSuggestion,
  streamChat,
  uploadDocuments,
} from './api';
import type {
  BatchIngestResponse,
  ChatMessage,
  DocumentSummary,
  FormDownloadRef,
  FormMappingSuggestion,
  SourceRef,
} from './types';

type Page = 'chat' | 'documents' | 'form-mapping';

const starterMessages: ChatMessage[] = [
  {
    id: crypto.randomUUID(),
    role: 'assistant',
    content: 'Ask about policies, procedures, facilities, forms, or HR guidance. I will answer from indexed documents and cite the source.',
  },
];

const chatPromptSuggestions = [
  'How do I submit a payment request and where can I download the form?',
  'How many additional annual leave days do I get based on seniority?',
  'How can I refer a candidate via Oracle?',
];

export default function App() {
  const [page, setPage] = useState<Page>('chat');
  const [suggestions, setSuggestions] = useState<FormMappingSuggestion[]>([]);
  const [suggestionsLoading, setSuggestionsLoading] = useState(false);
  const [suggestionsError, setSuggestionsError] = useState('');
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState('');

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

  useEffect(() => {
    void loadSuggestions();
    void loadDocuments();
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
          <button className={page === 'form-mapping' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('form-mapping')}>
            <FileText size={18} aria-hidden="true" />
            <span>Form mapping</span>
          </button>
        </nav>

        <div className="sidebar-status">
          <span className="status-dot" />
          Backend via Vite proxy
        </div>
      </aside>

      <main className="main-surface">
        {page === 'chat' && <ChatPage />}
        {page === 'documents' && (
          <DocumentsPage
            suggestions={suggestions}
            setSuggestions={setSuggestions}
            reloadSuggestions={loadSuggestions}
            documents={documents}
            setDocuments={setDocuments}
            reloadDocuments={loadDocuments}
            documentsLoading={documentsLoading}
            documentsError={documentsError}
          />
        )}
        {page === 'form-mapping' && (
          <FormMappingPage
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

        <div className="prompt-suggestions" aria-label="Suggested questions">
          {chatPromptSuggestions.map((question) => (
            <button
              key={question}
              type="button"
              className="prompt-suggestion"
              onClick={() => void submitMessage(question)}
              disabled={isStreaming}
            >
              <Star size={15} aria-hidden="true" />
              <span>{question}</span>
            </button>
          ))}
        </div>

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
          {isAssistant ? <MarkdownMessage content={message.content} /> : message.content || ' '}
        </div>
        {isAssistant && (
          <SourcesPanel sources={message.sources ?? []} formDownloads={message.formDownloads ?? []} />
        )}
      </div>
    </article>
  );
}

type MarkdownBlock =
  | { type: 'paragraph'; text: string }
  | { type: 'ordered'; items: string[] }
  | { type: 'unordered'; items: string[] };

function MarkdownMessage({ content }: { content: string }) {
  const blocks = useMemo(() => parseMarkdownBlocks(content), [content]);

  if (blocks.length === 0) {
    return <span> </span>;
  }

  return (
    <div className="markdown-content">
      {blocks.map((block, index) => {
        if (block.type === 'ordered') {
          return (
            <ol key={index}>
              {block.items.map((item, itemIndex) => (
                <li key={`${index}-${itemIndex}`}>{renderInlineMarkdown(item)}</li>
              ))}
            </ol>
          );
        }

        if (block.type === 'unordered') {
          return (
            <ul key={index}>
              {block.items.map((item, itemIndex) => (
                <li key={`${index}-${itemIndex}`}>{renderInlineMarkdown(item)}</li>
              ))}
            </ul>
          );
        }

        return <p key={index}>{renderInlineMarkdown(block.text)}</p>;
      })}
    </div>
  );
}

function parseMarkdownBlocks(content: string) {
  const lines = content.replace(/\r\n/g, '\n').split('\n');
  const blocks: MarkdownBlock[] = [];
  let paragraph: string[] = [];
  let orderedItems: string[] = [];
  let unorderedItems: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push({ type: 'paragraph', text: paragraph.join(' ') });
      paragraph = [];
    }
  };

  const flushOrdered = () => {
    if (orderedItems.length > 0) {
      blocks.push({ type: 'ordered', items: orderedItems });
      orderedItems = [];
    }
  };

  const flushUnordered = () => {
    if (unorderedItems.length > 0) {
      blocks.push({ type: 'unordered', items: unorderedItems });
      unorderedItems = [];
    }
  };

  const flushLists = () => {
    flushOrdered();
    flushUnordered();
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      flushParagraph();
      flushLists();
      continue;
    }

    const orderedMatch = trimmed.match(/^\d+\.\s+(.*)$/);
    if (orderedMatch) {
      flushParagraph();
      flushUnordered();
      orderedItems.push(orderedMatch[1]);
      continue;
    }

    const unorderedMatch = trimmed.match(/^[-*]\s+(.*)$/);
    if (unorderedMatch) {
      flushParagraph();
      flushOrdered();
      unorderedItems.push(unorderedMatch[1]);
      continue;
    }

    if (/^SOURCES:/i.test(trimmed)) {
      flushParagraph();
      flushLists();
      blocks.push({ type: 'paragraph', text: trimmed });
      continue;
    }

    flushLists();
    paragraph.push(trimmed);
  }

  flushParagraph();
  flushLists();
  return blocks;
}

function renderInlineMarkdown(text: string) {
  const parts = text.split(/(\*\*[^*]+\*\*)/g).filter(Boolean);
  return parts.map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**')) {
      return <strong key={index}>{part.slice(2, -2)}</strong>;
    }

    return <span key={index}>{part}</span>;
  });
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
                <SourceChip source={source} />
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

function SourceChip({ source }: { source: SourceRef }) {
  const content = (
    <>
      <FileText size={14} aria-hidden="true" />
      <span>{source.file} - Page {source.page}</span>
    </>
  );

  if (source.downloadPath) {
    return (
      <a className="source-chip source-link" href={source.downloadPath} target="_blank" rel="noreferrer">
        {content}
      </a>
    );
  }

  return <div className="source-chip">{content}</div>;
}

function DownloadLink({ download }: { download: FormDownloadRef }) {
  return (
    <a className="download-link" href={download.downloadPath}>
      <Download size={14} aria-hidden="true" />
      {download.formName}
    </a>
  );
}

function DocumentsPage({
  suggestions,
  setSuggestions,
  reloadSuggestions,
  documents,
  setDocuments,
  reloadDocuments,
  documentsLoading,
  documentsError,
}: {
  suggestions: FormMappingSuggestion[];
  setSuggestions: (suggestions: FormMappingSuggestion[]) => void;
  reloadSuggestions: () => Promise<void>;
  documents: DocumentSummary[];
  setDocuments: (documents: DocumentSummary[]) => void;
  reloadDocuments: () => Promise<void>;
  documentsLoading: boolean;
  documentsError: string;
}) {
  const [files, setFiles] = useState<File[]>([]);
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [fileTypeFilter, setFileTypeFilter] = useState('all');
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [uploadResult, setUploadResult] = useState<BatchIngestResponse | null>(null);
  const [deletingDocument, setDeletingDocument] = useState('');

  const documentStats = useMemo(() => {
    const typeCounts = documents.reduce<Record<string, { count: number; chunks: number }>>((acc, document) => {
      const type = (document.fileType || 'unknown').toUpperCase();
      acc[type] = acc[type] ?? { count: 0, chunks: 0 };
      acc[type].count += 1;
      acc[type].chunks += document.chunkCount;
      return acc;
    }, {});

    return {
      totalDocuments: documents.length,
      totalChunks: documents.reduce((sum, document) => sum + document.chunkCount, 0),
      types: Object.entries(typeCounts).sort(([left], [right]) => left.localeCompare(right)),
    };
  }, [documents]);

  const filteredDocuments = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    return documents.filter((document) => {
      const type = (document.fileType || 'unknown').toUpperCase();
      const matchesType = fileTypeFilter === 'all' || type === fileTypeFilter;
      const matchesQuery = !query
        || document.sourceFile.toLowerCase().includes(query)
        || type.toLowerCase().includes(query);

      return matchesType && matchesQuery;
    });
  }, [documents, fileTypeFilter, searchTerm]);

  const handleFileSelection = (fileList: FileList | null) => {
    setFiles(Array.from(fileList ?? []));
    setUploadResult(null);
    setUploadError('');
  };

  const handleUpload = async (event: FormEvent) => {
    event.preventDefault();
    if (files.length === 0 || uploading) {
      return;
    }

    setUploading(true);
    setUploadError('');
    setUploadResult(null);
    try {
      const result = await uploadDocuments(files);
      setUploadResult(result);
      const newSuggestions = result.results?.flatMap((item) => item.formMappingSuggestion ? [item.formMappingSuggestion] : []) ?? [];
      if (newSuggestions.length > 0) {
        const existingIds = new Set(newSuggestions.map((item) => item.id));
        setSuggestions([...newSuggestions, ...suggestions.filter((item) => !existingIds.has(item.id))]);
      } else {
        await reloadSuggestions();
      }
      setFiles([]);
      setUploadModalOpen(false);
      await reloadDocuments();
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
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
    <section className="documents-layout">
      <aside className="documents-sidebar">
        <h2>Statistics</h2>
        <div className="stat-grid">
          <div className="stat-card">
            <strong>{documentStats.totalDocuments}</strong>
            <span>Documents</span>
          </div>
          <div className="stat-card">
            <strong>{documentStats.totalChunks}</strong>
            <span>Chunks</span>
          </div>
        </div>

        <div className="file-type-filter">
          <h2>File Type</h2>
          <button
            className={fileTypeFilter === 'all' ? 'file-type-row active' : 'file-type-row'}
            type="button"
            onClick={() => setFileTypeFilter('all')}
          >
            <span>All</span>
            <strong>{documentStats.totalDocuments}</strong>
          </button>
          {documentStats.types.map(([type, stats]) => (
            <button
              className={fileTypeFilter === type ? 'file-type-row active' : 'file-type-row'}
              key={type}
              type="button"
              onClick={() => setFileTypeFilter(type)}
            >
              <span>{type}</span>
              <strong>{stats.count}</strong>
            </button>
          ))}
        </div>
      </aside>

      <section className="documents-main">
        <div className="documents-toolbar">
          <label className="search-box" htmlFor="document-search">
            <Search size={18} aria-hidden="true" />
            <input
              id="document-search"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="Search documents..."
            />
          </label>

          <div className="document-actions">
            <button className="secondary-button" onClick={() => void reloadDocuments()} disabled={documentsLoading}>
              <RefreshCw size={16} className={documentsLoading ? 'spin' : ''} aria-hidden="true" />
              Refresh
            </button>
            <button className="primary-button upload-modal-button" onClick={() => setUploadModalOpen(true)}>
              <Upload size={16} aria-hidden="true" />
              Upload document
            </button>
          </div>
        </div>

        {(uploadError || uploadResult) && (
          <section className="upload-feedback">
            {uploadError && <ErrorBanner message={uploadError} />}
            {uploadResult && (
              <div className="success-box">
                <Check size={16} aria-hidden="true" />
                <div>
                  <strong>{uploadResult.message ?? 'Documents processed.'}</strong>
                  <span>{uploadResult.succeeded ?? 0} succeeded - {uploadResult.failed ?? 0} failed</span>
                </div>
              </div>
            )}
            {uploadResult?.results?.some((item) => item.formMappingSuggestion) && (
              <div className="empty-state small">
                New form mapping suggestion is ready in the Form mapping tab.
              </div>
            )}
          </section>
        )}

        <section className="documents-table-section">
          {documentsError && <ErrorBanner message={documentsError} />}
          <DocumentLibrary
            documents={filteredDocuments}
            loading={documentsLoading}
            deletingDocument={deletingDocument}
            onDelete={handleDeleteDocument}
          />
        </section>

        {uploadModalOpen && (
          <div className="modal-backdrop" role="presentation">
            <form className="upload-dialog" onSubmit={handleUpload} aria-label="Upload document">
              <div className="modal-heading">
                <h2>Upload Document</h2>
                <button
                  type="button"
                  className="icon-button ghost"
                  onClick={() => {
                    setUploadModalOpen(false);
                    setFiles([]);
                  }}
                  aria-label="Close upload dialog"
                >
                  <X size={18} aria-hidden="true" />
                </button>
              </div>

              <label
                className="dropzone"
                htmlFor="document-files"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => {
                  event.preventDefault();
                  handleFileSelection(event.dataTransfer.files);
                }}
              >
                <Upload size={36} aria-hidden="true" />
                <span>Drop files here or click to browse</span>
              </label>
              <input
                id="document-files"
                className="sr-only"
                type="file"
                multiple
                accept=".pdf,.docx,.doc,.xlsx,.pptx"
                onChange={(event) => handleFileSelection(event.target.files)}
              />

              {files.length > 0 && (
                <div className="selected-files">
                  {files.map((selectedFile) => (
                    <span key={`${selectedFile.name}-${selectedFile.size}`}>{selectedFile.name}</span>
                  ))}
                </div>
              )}

              {uploadError && <ErrorBanner message={uploadError} />}

              <div className="modal-actions">
                <button
                  type="button"
                  className="secondary-button"
                  onClick={() => {
                    setUploadModalOpen(false);
                    setFiles([]);
                  }}
                >
                  Cancel
                </button>
                <button className="primary-button" type="submit" disabled={files.length === 0 || uploading}>
                  {uploading ? <Loader2 size={16} className="spin" aria-hidden="true" /> : <Upload size={16} aria-hidden="true" />}
                  Upload
                </button>
              </div>
            </form>
          </div>
        )}
      </section>
    </section>
  );
}

function FormMappingPage({
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
  const upsertSuggestion = (updated: FormMappingSuggestion) => {
    setSuggestions(suggestions.map((suggestion) => (suggestion.id === updated.id ? updated : suggestion)));
  };

  return (
    <section className="page-grid form-mapping-layout">
      <div className="page-header">
        <div>
          <p className="eyebrow">Review queue</p>
          <h1>Form mapping</h1>
        </div>
        <button className="secondary-button" onClick={() => void reloadSuggestions()} disabled={suggestionsLoading}>
          <RefreshCw size={16} className={suggestionsLoading ? 'spin' : ''} aria-hidden="true" />
          Refresh
        </button>
      </div>

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
    </section>
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
                {document.downloadPath ? (
                  <a className="document-name document-link" href={document.downloadPath} target="_blank" rel="noreferrer">
                    <FileText size={15} aria-hidden="true" />
                    <span title={document.sourceFile}>{document.sourceFile}</span>
                  </a>
                ) : (
                  <div className="document-name">
                    <FileText size={15} aria-hidden="true" />
                    <span title={document.sourceFile}>{document.sourceFile}</span>
                  </div>
                )}
              </td>
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
