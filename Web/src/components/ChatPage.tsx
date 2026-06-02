import { FormEvent, useMemo, useRef, useState } from 'react';
import { Download, Image, Loader2, Send, Sparkles } from 'lucide-react';
import { streamChat, toAssetUrl } from '../api';
import type { ChatMessage, FormDownloadRef, SourceRef } from '../types';

const starterPrompts = [
  'What is the annual leave policy?',
  'How do I submit a payment request?',
  'What does the CII emergency response flowchart show?',
];

export function ChatPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: "Hello. Ask me anything about ELCA company policies, procedures, forms, or CII Tower support.",
    },
  ]);
  const [input, setInput] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [isWaitingForFirstToken, setIsWaitingForFirstToken] = useState(false);
  const [error, setError] = useState('');
  const abortRef = useRef<AbortController | null>(null);

  const canSend = useMemo(() => input.trim().length > 0 && !isStreaming, [input, isStreaming]);

  async function sendMessage(messageText: string) {
    const trimmed = messageText.trim();
    if (!trimmed || isStreaming) {
      return;
    }

    const assistantId = crypto.randomUUID();
    setInput('');
    setError('');
    setIsStreaming(true);
    setIsWaitingForFirstToken(true);
    setMessages((current) => [
      ...current,
      { id: crypto.randomUUID(), role: 'user', content: trimmed },
      { id: assistantId, role: 'assistant', content: '' },
    ]);

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      await streamChat(
        trimmed,
        {
          onToken: (token) => {
            setIsWaitingForFirstToken(false);
            setMessages((current) =>
              current.map((message) =>
                message.id === assistantId ? { ...message, content: message.content + token } : message,
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
        controller.signal,
      );
    } catch (exception) {
      if (!controller.signal.aborted) {
        setError(exception instanceof Error ? exception.message : 'Chat request failed.');
      }
    } finally {
      setIsStreaming(false);
      setIsWaitingForFirstToken(false);
      abortRef.current = null;
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void sendMessage(input);
  }

  function stopStreaming() {
    abortRef.current?.abort();
  }

  return (
    <section className="page chat-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Policy RAG</p>
          <h1>Chat</h1>
        </div>
        <div className="status-pill">
          <span className="status-dot" />
          Live
        </div>
      </header>

      <div className="chat-surface">
        <div className="messages" aria-live="polite">
          {messages.map((message) => (
            <MessageBubble key={message.id} message={message} />
          ))}
          {isWaitingForFirstToken ? (
            <div className="message-row assistant">
              <div className="message-bubble loading-bubble">
                <Loader2 size={16} className="spin" />
                Searching policy documents
              </div>
            </div>
          ) : null}
        </div>

        <div className="prompt-strip">
          {starterPrompts.map((prompt) => (
            <button key={prompt} type="button" onClick={() => void sendMessage(prompt)} disabled={isStreaming}>
              <Sparkles size={14} />
              {prompt}
            </button>
          ))}
        </div>

        {error ? <div className="error-banner">{error}</div> : null}

        <form className="composer" onSubmit={handleSubmit}>
          <textarea
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Ask about leave, payments, CII Tower, forms, or procedures"
            rows={2}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void sendMessage(input);
              }
            }}
          />
          {isStreaming ? (
            <button type="button" className="secondary-button" onClick={stopStreaming}>
              Stop
            </button>
          ) : (
            <button type="submit" className="primary-button" disabled={!canSend} title="Send message">
              <Send size={18} />
              Send
            </button>
          )}
        </form>
      </div>
    </section>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  return (
    <div className={`message-row ${message.role}`}>
      <article className="message-bubble">
        {message.content ? <p>{message.content}</p> : <p className="muted-text">Preparing answer...</p>}
        {message.role === 'assistant' ? (
          <SourcesPanel sources={message.sources ?? []} downloads={message.formDownloads ?? []} />
        ) : null}
      </article>
    </div>
  );
}

function SourcesPanel({ sources, downloads }: { sources: SourceRef[]; downloads: FormDownloadRef[] }) {
  if (sources.length === 0 && downloads.length === 0) {
    return null;
  }

  return (
    <div className="sources-panel">
      {sources.length > 0 ? (
        <div className="source-list">
          {sources.map((source, index) => (
            <div className="source-item" key={`${source.file}-${source.page}-${index}`}>
              <span className="source-chip">
                {source.file} - Page {source.page}
              </span>
              {source.chunkType === 'image_caption' ? <span className="source-kind">Image</span> : null}
              {source.formDownload ? <DownloadLink download={source.formDownload} /> : null}
              <SourceImages source={source} />
            </div>
          ))}
        </div>
      ) : null}

      {downloads.length > 0 ? (
        <div className="download-list">
          {downloads.map((download) => (
            <DownloadLink key={download.downloadPath} download={download} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function SourceImages({ source }: { source: SourceRef }) {
  const paths = [source.imagePath, ...(source.imagePaths ?? [])].filter(Boolean) as string[];
  const uniquePaths = [...new Set(paths)];

  if (uniquePaths.length === 0) {
    return null;
  }

  return (
    <div className="source-images">
      {uniquePaths.map((path) => (
        <figure key={path}>
          <img src={toAssetUrl(path)} alt={`${source.file}, page ${source.page}`} loading="lazy" />
          <figcaption>
            <Image size={13} />
            {source.file} - Page {source.page}
          </figcaption>
        </figure>
      ))}
    </div>
  );
}

function DownloadLink({ download }: { download: FormDownloadRef }) {
  return (
    <a className="download-link" href={toAssetUrl(download.downloadPath)}>
      <Download size={14} />
      {download.formName}
    </a>
  );
}
