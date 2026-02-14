import {Moment} from 'moment';

export interface UserEntity {
  id: string;
  createdAt: Moment;
  deletedAt: Moment | null;
  accounts: AccountEntity[];
}

export interface AccountEntity {
  id: string;
  providerId: string;
  name: string | undefined;
  email: string | undefined;
  avatarUrl: string | undefined;
}
