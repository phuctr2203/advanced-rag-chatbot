export type SourceRef = {
  file: string;
  downloadPath?: string;
  page: number;
  chunkType?: string;
  imagePath?: string;
  imagePaths?: string[];
  formDownload?: FormDownloadRef | null;
};

export type FormDownloadRef = {
  formName: string;
  downloadPath: string;
};

export type SourcesPayload = {
  sources: SourceRef[];
  formDownloads?: FormDownloadRef[];
};

export type ChatMessage = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  sources?: SourceRef[];
  formDownloads?: FormDownloadRef[];
};

export type FormMappingSuggestion = {
  id: string;
  formName: string;
  aliases: string[];
  candidateTemplateFile: string;
  candidateDownloadPath: string;
  agent: string;
  confidence: number;
  reason: string;
  status: 'pending_review' | 'needs_manual_review' | 'accepted' | 'rejected';
};

export type IngestResponse = {
  message?: string;
  fileName?: string;
  status?: string;
  agent?: string;
  chunks?: number;
  replacedChunks?: number;
  isUpdate?: boolean;
  skipped?: boolean;
  fileHash?: string;
  ingestedAt?: string;
  formMappingSuggestion?: FormMappingSuggestion | null;
  error?: string;
};

export type BatchIngestResponse = {
  message?: string;
  succeeded?: number;
  failed?: number;
  results?: IngestResponse[];
  error?: string;
};

export type AgentValue = '' | 'ELCA_HR' | 'ELCA_GENERAL' | 'CII_TOWER_SUPPORT';

export type DocumentSummary = {
  sourceFile: string;
  downloadPath?: string;
  agent: string;
  fileType: string;
  chunkCount: number;
  pageCount: number;
  hasImages: boolean;
  hasFormTemplate: boolean;
};
