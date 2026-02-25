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
  fields: string[];
  url?: string;
}

export interface ProviderParametersEntity {
  clientName?: string;
  clientId?: string;
  clientSecret?: string;
  providerUrl?: string;
  saveTokens: boolean;
  defaultScope: string[];
}
