import {Moment} from 'moment';

export interface ApplicationEntity {
  id: string;
  clientId: string;
  clientSecret: string;
  parameters: ApplicationParametersEntity;
  createdAt: Moment;
  deletedAt: Moment | null;
}

export interface ApplicationParametersEntity {
  name: string;
  redirectUrls: string[];
}
