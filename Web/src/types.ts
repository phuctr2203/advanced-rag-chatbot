export type SourceRef = {
  file: string;
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
  formMappingSuggestion?: FormMappingSuggestion | null;
  error?: string;
};

export type AgentValue = '' | 'ELCA_HR' | 'ELCA_GENERAL' | 'CII_TOWER_SUPPORT';

export type DocumentSummary = {
  sourceFile: string;
  agent: string;
  fileType: string;
  chunkCount: number;
  pageCount: number;
  hasImages: boolean;
  hasFormTemplate: boolean;
};

export type CurrentProviderResponse = {
  llmProvider: string;
  llmModel: string;
  visionProvider: string;
  visionModel: string;
  embeddingProvider: string;
  embeddingBaseUrl: string;
};

export type ServiceStatus = {
  name: string;
  healthy: boolean;
  message: string;
};

export type ProviderStatusResponse = {
  llm: ServiceStatus;
  embedding: ServiceStatus;
  qdrant: ServiceStatus;
};
