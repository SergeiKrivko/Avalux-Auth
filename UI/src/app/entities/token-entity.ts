import {Moment} from 'moment';

export interface TokenEntity {
  id: string;
  name?: string;
  permissions: string[];
  createdAt?: Moment;
  expiresAt?: Moment;
}

export interface TokenPermissionEntity {
  key: string;
  description: string;
}
