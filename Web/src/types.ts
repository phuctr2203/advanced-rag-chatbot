export type SourceRef = {
  file: string;
  page: number;
  chunkType?: 'text' | 'image_caption' | string;
  imagePath?: string;
  imagePaths?: string[];
  formDownload?: FormDownloadRef | null;
};

export type FormDownloadRef = {
  formName: string;
  downloadPath: string;
};

export type ChatMessage = {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  sources?: SourceRef[];
  formDownloads?: FormDownloadRef[];
};

export type UploadAgent = '' | 'ELCA_HR' | 'ELCA_GENERAL' | 'CII_TOWER_SUPPORT';
