import {Moment} from 'moment';
import {UserSubscriptionEntity} from './subscription-entity';

export interface UserEntity {
  id: string;
  createdAt: Moment;
  deletedAt: Moment | null;
  accounts: AccountEntity[];
  subscriptions: UserSubscriptionEntity[];
}

export interface AccountEntity {
  id: string;
  providerId: string;
  name: string | undefined;
  email: string | undefined;
  avatarUrl: string | undefined;
}
