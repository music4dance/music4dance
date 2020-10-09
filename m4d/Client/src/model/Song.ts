/* tslint:disable:max-classes-per-file */
import 'reflect-metadata';
import { jsonMember, jsonObject, jsonArrayMember } from 'typedjson';
import { DanceEnvironment } from './DanceEnvironmet';
import { Tag } from './Tag';
import { SongHistory } from './SongHistory';
import { SongProperty, PropertyType } from './SongProperty';
import { pascalToCamel } from '@/helpers/StringHelpers';
import { PurchaseEncoded, ServiceType, PurchaseInfo } from './Purchase';
import { enumKeys } from '@/helpers/enumKeys';
import { timeOrder, timeOrderVerbose } from '@/helpers/timeHelpers';
import { ITaggableObject } from './ITaggableObject';
import { DanceStats } from './DanceStats';

declare const environment: DanceEnvironment;

@jsonObject export class DanceRating implements ITaggableObject {
    @jsonMember public danceId!: string;
    @jsonMember public weight!: number;
    @jsonArrayMember(Tag) public tags!: Tag[];
    @jsonArrayMember(Tag) public currentUserTags!: Tag[];

    public constructor(init?: Partial<DanceRating>) {
        Object.assign(this, init);
    }

    public get id(): string {
        return this.danceId;
    }

    public get positiveTag(): Tag {
        return new Tag({value: this.stats.danceName, category: 'Dance'});
    }

    public get negativeTag(): Tag {
        return new Tag({value: '!' + this.stats.danceName, category: 'Dance'});
    }

    public get description(): string {
        return environment.fromId(this.danceId)!.danceName;
    }

    public get stats(): DanceStats {
        return environment.fromId(this.danceId)!;
    }
}

@jsonObject export class ModifiedRecord {
    @jsonMember public userName!: string;
    @jsonMember public like?: boolean;
    @jsonMember public owned?: number;

    public constructor(init?: Partial<ModifiedRecord>) {
        Object.assign(this, init);
    }
}

@jsonObject export class AlbumDetails {
    @jsonMember public name!: string;
    @jsonMember public track?: number;
    @jsonMember public purchase!: PurchaseEncoded;

    public constructor(init?: Partial<AlbumDetails>) {
        Object.assign(this, init);
    }
}

@jsonObject export class Song implements ITaggableObject {
    public static FromHistory(history: SongHistory): Song {
        const song = new Song();
        song.songId = history.id;
        song.loadProperties(history.properties);
        return song;
    }

    @jsonMember public songId!: string;
    @jsonMember public title!: string;
    @jsonMember public artist!: string;
    @jsonMember public tempo?: number;
    @jsonMember public length?: number;
    @jsonMember public sample?: string;
    @jsonMember public danceability?: number;
    @jsonMember public energy?: number;
    @jsonMember public valence?: number;
    @jsonMember public created!: Date;
    @jsonMember public modified!: Date;
    @jsonArrayMember(Tag) public tags!: Tag[];
    @jsonArrayMember(Tag) public currentUserTags!: Tag[];
    @jsonArrayMember(DanceRating) public danceRatings!: DanceRating[];
    @jsonArrayMember(ModifiedRecord) public modifiedBy!: ModifiedRecord[];
    @jsonArrayMember(AlbumDetails) public albums!: AlbumDetails[];

    public constructor(init?: Partial<Song>) {
        Object.assign(this, init);
    }

    public getPurchaseInfo(service: ServiceType): PurchaseInfo | undefined {
        const album = this.albums.find((a) => a.purchase.decodeService(service));
        if (album) {
            return album.purchase.decodeService(service);
        }
        return undefined;
    }

    public getPurchaseInfos(): PurchaseInfo[] {
        const ret = [];

        for (const service of enumKeys(ServiceType)) {
            const purchase = this.getPurchaseInfo(ServiceType[service]);
            if (purchase) {
                ret.push(purchase);
            }
        }

        return ret;
    }

    public findDanceRatingById(id: string): DanceRating | undefined {
        return this.danceRatings.find((r) => r.danceId === id)!;
    }

    public findDanceRatingByName(name: string): DanceRating | undefined {
        const ds = environment!.fromName(name)!;
        return this.findDanceRatingById(ds.danceId);
    }

    public get createdOrder(): string {
        return timeOrder(this.created);
    }

    public get modifiedOrder(): string {
        return timeOrder(this.modified);
    }

    public get createdOrderVerbose(): string {
        return timeOrderVerbose(this.created);
    }

    public get modifiedOrderVerbose(): string {
        return timeOrderVerbose(this.modified);
    }

    public get id(): string {
        return this.songId;
    }

    public get description(): string {
        return `"${this.title}" by ${this.artist}`;
    }

    public getUserModified(userName?: string): ModifiedRecord | undefined {
        if (!userName) {
            return undefined;
        }
        const name = userName.toLowerCase();
        return this.modifiedBy.find((mr) => mr.userName.toLowerCase() === name);
    }

    private loadProperties(properties: SongProperty[]): void {
        let created: boolean = false;
        let user: string;
        let currentModified: ModifiedRecord;
        let deleted: boolean = false;

        properties.forEach((property) => {
            const baseName = property.baseName;

            switch (baseName) {
                case PropertyType.userField:
                case PropertyType.userProxy:
                    user = property.value;
                    currentModified = new ModifiedRecord({ userName: user });
                    break;
                case PropertyType.danceRatingField:
                    // UpdateDanceRating
                    break;
                case PropertyType.addedTags:
                    // Add Tags
                    break;
                case PropertyType.removedTags:
                    // Remove tags
                    break;
                case PropertyType.albumField:
                case PropertyType.publisherField:
                case PropertyType.trackField:
                case PropertyType.purchaseField:
                    // All of these are taken care of with build album
                    break;
                case PropertyType.deleteCommand:
                    deleted = true; // TODO: Some more login in the C# code to check
                    break;
                case PropertyType.timeField:
                    if (!created) {
                        this.created = property.valueTyped as Date;
                        created = true;
                    }
                    this.modified = property.valueTyped as Date;
                    break;
                case PropertyType.ownerHash:
                    if (currentModified) {
                        currentModified.owned = property.valueTyped as number;
                    }
                    break;
                case PropertyType.likeTag:
                    if (currentModified) {
                        currentModified.like = property.valueTyped as boolean | undefined;
                    }
                    break;
                default:
                    if (!property.isAction) {
                        (this as any)[pascalToCamel(baseName)] = property.valueTyped;
                    }
                    break;
            }
        });
    }
}

