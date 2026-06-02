import { useState } from 'react';
import { FileText, MessageSquare } from 'lucide-react';
import { ChatPage } from './components/ChatPage';
import { DocumentsPage } from './components/DocumentsPage';

type Page = 'chat' | 'documents';

export function App() {
  const [page, setPage] = useState<Page>('chat');

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">E</div>
          <div>
            <div className="brand-title">ELCA Policy</div>
            <div className="brand-subtitle">Assistant</div>
          </div>
        </div>

        <nav className="nav-list" aria-label="Main navigation">
          <button className={page === 'chat' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('chat')}>
            <MessageSquare size={18} />
            <span>Chat</span>
          </button>
          <button className={page === 'documents' ? 'nav-item active' : 'nav-item'} onClick={() => setPage('documents')}>
            <FileText size={18} />
            <span>Documents</span>
          </button>
        </nav>
      </aside>

      <main className="main-panel">{page === 'chat' ? <ChatPage /> : <DocumentsPage />}</main>
    </div>
  );
}
