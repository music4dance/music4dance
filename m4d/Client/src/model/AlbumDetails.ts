import "reflect-metadata";
import { jsonMember, jsonObject } from "typedjson";
import { PurchaseEncoded } from "./Purchase";

@jsonObject
export class AlbumDetails {
  @jsonMember public index?: number;
  @jsonMember public name?: string;
  @jsonMember public track?: number;
  @jsonMember public publisher?: string;
  @jsonMember public purchase!: PurchaseEncoded;

  public constructor(init?: Partial<AlbumDetails>) {
    Object.assign(this, init);
    if (!this.purchase) {
      this.purchase = new PurchaseEncoded();
    }
  }
}
