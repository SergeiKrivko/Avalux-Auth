import {Moment} from 'moment';

export interface ProviderEntity {
  id: string;
  providerId: number;
  applicationId: string;
  parameters: ProviderParametersEntity;
  createdAt: Moment;
  deletedAt: Moment | null;
}

export interface ProviderInfoEntity {
  name: string;
  key: string;
  id: number;
  url?: string;
}

export interface ProviderParametersEntity {
  clientId?: string;
  clientSecret?: string;
  saveTokens: boolean;
  defaultScope: string[];
}
