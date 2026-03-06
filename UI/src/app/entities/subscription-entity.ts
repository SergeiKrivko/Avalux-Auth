import {Moment} from 'moment';
import {MoneyEntity} from './money-entity';

export interface SubscriptionPlanEntity {
  id: string;
  applicationId: string;
  info: SubscriptionPlanInfoEntity;
  createdAt?: Moment;
}

export interface SubscriptionPlanInfoEntity {
  key: string;
  displayName?: string;
  description?: string;
  advantages: string[];
  isHidden: boolean;
  isDefault: boolean;
  price: MoneyEntity,
}

export interface UserSubscriptionEntity {
  id: string;
  userId: string;
  planId: string;
  createdAt?: Moment;
  expiresAt?: Moment;
}
