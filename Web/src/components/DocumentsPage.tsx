import { ChangeEvent, FormEvent, useCallback, useEffect, useState } from 'react';
import { CheckCircle2, FileText, FileUp, Loader2, RefreshCw, Trash2, Upload } from 'lucide-react';
import { deleteDocument, fetchCurrentProvider, fetchDocuments, fetchProviderStatus, uploadDocument } from '../api';
import type { CurrentProvider, DocumentSummary, ProviderStatus, ServiceStatus, UploadAgent } from '../types';

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
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState(true);
  const [documentsError, setDocumentsError] = useState('');
  const [deletingSourceFile, setDeletingSourceFile] = useState('');
  const [currentProvider, setCurrentProvider] = useState<CurrentProvider | null>(null);
  const [providerStatus, setProviderStatus] = useState<ProviderStatus | null>(null);
  const [isLoadingProviderStatus, setIsLoadingProviderStatus] = useState(true);
  const [providerError, setProviderError] = useState('');

  const loadDocuments = useCallback(async () => {
    setIsLoadingDocuments(true);
    setDocumentsError('');

    try {
      setDocuments(await fetchDocuments());
    } catch (exception) {
      setDocumentsError(exception instanceof Error ? exception.message : 'Could not load documents.');
    } finally {
      setIsLoadingDocuments(false);
    }
  }, []);

  const loadProviderStatus = useCallback(async () => {
    setIsLoadingProviderStatus(true);
    setProviderError('');

    try {
      const [provider, status] = await Promise.all([fetchCurrentProvider(), fetchProviderStatus()]);
      setCurrentProvider(provider);
      setProviderStatus(status);
    } catch (exception) {
      setProviderError(exception instanceof Error ? exception.message : 'Could not load provider status.');
    } finally {
      setIsLoadingProviderStatus(false);
    }
  }, []);

  useEffect(() => {
    void loadDocuments();
    void loadProviderStatus();
  }, [loadDocuments, loadProviderStatus]);

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    setFile(event.target.files?.[0] ?? null);
    setError('');
    setResult(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || isUploading) {
      return;
    }

    const form = event.currentTarget;
    setIsUploading(true);
    setError('');
    setResult(null);

    try {
      const uploadResult = await uploadDocument(file, agent);
      setResult(uploadResult);
      setFile(null);
      form.reset();
      await loadDocuments();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Upload failed.');
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDelete(sourceFile: string) {
    if (deletingSourceFile || !window.confirm(`Delete "${sourceFile}" from the document library?`)) {
      return;
    }

    setDeletingSourceFile(sourceFile);
    setDocumentsError('');

    try {
      await deleteDocument(sourceFile);
      await loadDocuments();
    } catch (exception) {
      setDocumentsError(exception instanceof Error ? exception.message : 'Could not delete document.');
    } finally {
      setDeletingSourceFile('');
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
          <div className="status-header">
            <h2>Status</h2>
            <button
              className="icon-button"
              type="button"
              title="Refresh provider status"
              aria-label="Refresh provider status"
              onClick={() => void loadProviderStatus()}
              disabled={isLoadingProviderStatus}
            >
              {isLoadingProviderStatus ? <Loader2 size={16} className="spin" /> : <RefreshCw size={16} />}
            </button>
          </div>
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

          {providerError ? <div className="provider-error">{providerError}</div> : null}

          <div className="provider-health">
            <StatusChip label="LLM" status={providerStatus?.llm} isLoading={isLoadingProviderStatus} />
            <StatusChip label="Embedding" status={providerStatus?.embedding} isLoading={isLoadingProviderStatus} />
            <StatusChip label="Qdrant" status={providerStatus?.qdrant} isLoading={isLoadingProviderStatus} />
          </div>

          {currentProvider ? (
            <div className="system-notes">
              <ProviderMetric label="LLM" value={`${currentProvider.llmProvider} / ${currentProvider.llmModel}`} />
              <ProviderMetric label="Vision" value={`${currentProvider.visionProvider} / ${currentProvider.visionModel}`} />
              <ProviderMetric label="Embedding" value={`${currentProvider.embeddingProvider} / ${currentProvider.embeddingBaseUrl}`} />
            </div>
          ) : null}
        </aside>
      </div>

      <section className="library-panel">
        <div className="library-header">
          <div>
            <p className="eyebrow">Indexed content</p>
            <h2>Document library</h2>
          </div>
          <button className="secondary-button refresh-button" type="button" onClick={loadDocuments} disabled={isLoadingDocuments}>
            {isLoadingDocuments ? <Loader2 size={17} className="spin" /> : <RefreshCw size={17} />}
            Refresh
          </button>
        </div>

        {documentsError ? <div className="error-banner library-error">{documentsError}</div> : null}

        {isLoadingDocuments ? (
          <div className="library-state">
            <Loader2 size={18} className="spin" />
            Loading documents
          </div>
        ) : documents.length === 0 ? (
          <div className="library-state">No indexed documents found.</div>
        ) : (
          <div className="documents-table-wrap">
            <table className="documents-table">
              <thead>
                <tr>
                  <th>Document</th>
                  <th>Agent</th>
                  <th>Type</th>
                  <th>Chunks</th>
                  <th>Pages</th>
                  <th>Images</th>
                  <th>Form</th>
                  <th className="actions-heading">Actions</th>
                </tr>
              </thead>
              <tbody>
                {documents.map((document) => (
                  <tr key={document.sourceFile}>
                    <td>
                      <div className="document-name">
                        <FileText size={18} />
                        <span title={document.sourceFile}>{document.sourceFile}</span>
                      </div>
                    </td>
                    <td>{formatAgent(document.agent)}</td>
                    <td>{document.fileType || '-'}</td>
                    <td>{document.chunkCount}</td>
                    <td>{document.pageCount || '-'}</td>
                    <td>
                      <span className={document.hasImages ? 'boolean-pill yes' : 'boolean-pill'}>{document.hasImages ? 'Yes' : 'No'}</span>
                    </td>
                    <td>
                      <span className={document.hasFormTemplate ? 'boolean-pill yes' : 'boolean-pill'}>
                        {document.hasFormTemplate ? 'Yes' : 'No'}
                      </span>
                    </td>
                    <td className="actions-cell">
                      <button
                        className="icon-button danger-button"
                        type="button"
                        title={`Delete ${document.sourceFile}`}
                        aria-label={`Delete ${document.sourceFile}`}
                        onClick={() => void handleDelete(document.sourceFile)}
                        disabled={Boolean(deletingSourceFile)}
                      >
                        {deletingSourceFile === document.sourceFile ? <Loader2 size={17} className="spin" /> : <Trash2 size={17} />}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  );
}

function formatAgent(agent: string) {
  return agent.replaceAll('_', ' ') || '-';
}

function ProviderMetric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="metric-label">{label}</span>
      <strong title={value}>{value}</strong>
    </div>
  );
}

function StatusChip({ label, status, isLoading }: { label: string; status?: ServiceStatus; isLoading: boolean }) {
  const healthy = Boolean(status?.healthy);
  const message = isLoading ? 'Checking status' : status?.message || 'Unavailable';
  const className = isLoading ? 'health-chip checking' : healthy ? 'health-chip healthy' : 'health-chip';

  return (
    <span className={className} title={`${label}: ${message}`}>
      <span className="health-dot" />
      {label}
    </span>
  );
}
