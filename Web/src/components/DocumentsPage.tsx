import { ChangeEvent, FormEvent, useState } from 'react';
import { CheckCircle2, FileUp, Loader2, Upload } from 'lucide-react';
import { uploadDocument } from '../api';
import type { UploadAgent } from '../types';

const agentOptions: Array<{ value: UploadAgent; label: string }> = [
  { value: '', label: 'Auto-detect' },
  { value: 'ELCA_HR', label: 'ELCA HR' },
  { value: 'ELCA_GENERAL', label: 'ELCA General' },
  { value: 'CII_TOWER_SUPPORT', label: 'CII Tower Support' },
];

export function DocumentsPage() {
  const [file, setFile] = useState<File | null>(null);
  const [agent, setAgent] = useState<UploadAgent>('');
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{ message: string; agent: string; chunks: number } | null>(null);

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    setFile(event.target.files?.[0] ?? null);
    setError('');
    setResult(null);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!file || isUploading) {
      return;
    }

    setIsUploading(true);
    setError('');
    setResult(null);

    try {
      const uploadResult = await uploadDocument(file, agent);
      setResult(uploadResult);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Upload failed.');
    } finally {
      setIsUploading(false);
    }
  }

  return (
    <section className="page documents-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Knowledge base</p>
          <h1>Documents</h1>
        </div>
      </header>

      <div className="documents-grid">
        <form className="upload-panel" onSubmit={handleSubmit}>
          <div className="upload-zone">
            <FileUp size={28} />
            <label htmlFor="document-upload">Choose a policy document</label>
            <input
              id="document-upload"
              type="file"
              accept=".pdf,.docx,.doc,.xlsx,.pptx"
              onChange={handleFileChange}
            />
            <span>{file ? file.name : 'PDF, DOCX, DOC, XLSX, or PPTX'}</span>
          </div>

          <label className="field-label" htmlFor="agent-select">
            Agent
          </label>
          <select
            id="agent-select"
            value={agent}
            onChange={(event) => setAgent(event.target.value as UploadAgent)}
          >
            {agentOptions.map((option) => (
              <option key={option.value || 'auto'} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          {error ? <div className="error-banner">{error}</div> : null}

          <button className="primary-button upload-button" type="submit" disabled={!file || isUploading}>
            {isUploading ? <Loader2 size={18} className="spin" /> : <Upload size={18} />}
            {isUploading ? 'Uploading' : 'Upload'}
          </button>
        </form>

        <aside className="upload-summary">
          <h2>Status</h2>
          {result ? (
            <div className="success-card">
              <CheckCircle2 size={22} />
              <div>
                <strong>{result.message}</strong>
                <span>Agent: {result.agent}</span>
                <span>Chunks: {result.chunks}</span>
              </div>
            </div>
          ) : (
            <div className="empty-state">Upload a document to add it to the policy index.</div>
          )}

          <div className="system-notes">
            <div>
              <span className="metric-label">Chat endpoint</span>
              <strong>/api/chat</strong>
            </div>
            <div>
              <span className="metric-label">Ingest endpoint</span>
              <strong>/api/ingest</strong>
            </div>
          </div>
        </aside>
      </div>
    </section>
  );
}
