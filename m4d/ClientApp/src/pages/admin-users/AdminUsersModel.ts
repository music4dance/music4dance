import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class AdminUserSummary {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public userName!: string;
  @jsonMember(String) public email?: string;
  @jsonMember(Boolean) public emailConfirmed!: boolean;
  @jsonMember(Boolean) public isPseudo!: boolean;
  @jsonMember(String) public startDate!: string;
  @jsonMember(String) public lastActive!: string;
  @jsonMember(Number) public hitCount!: number;
  @jsonMember(Number) public lifetimePurchased!: number;
  @jsonMember(Number) public subscriptionLevel!: number;
  @jsonMember(Number) public privacy!: number;
  @jsonMember(Number) public canContact!: number;
  @jsonMember(String) public servicePreference?: string;
  @jsonMember(Number) public failedCardAttempts!: number;
  @jsonArrayMember(String) public roles!: string[];
  @jsonArrayMember(String) public logins!: string[];
}

@jsonObject
export class AdminServiceInfo {
  @jsonMember(String) public cid!: string;
  @jsonMember(String) public name!: string;
}

@jsonObject
export class AdminUsersModel {
  @jsonArrayMember(AdminUserSummary) public users!: AdminUserSummary[];
  @jsonArrayMember(String) public allRoles!: string[];
  @jsonArrayMember(AdminServiceInfo) public services!: AdminServiceInfo[];
}
