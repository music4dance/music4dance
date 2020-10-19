/* tslint:disable:max-classes-per-file */
import 'reflect-metadata';
import { jsonMember, jsonObject } from 'typedjson';
import { enumKeys } from '@/helpers/enumKeys';

export enum ServiceType {
    Amazon = 'A',
    ITunes = 'I',
    Spotify = 'S',
}

export enum ServiceObjectType {
    Album = 'A',
    Song = 'S',
}

@jsonObject export class PurchaseInfo {
    public static Build(service: ServiceType, albumId?: string, songId?: string) {
        const info = { albumId, songId };
        switch (service) {
            case ServiceType.Amazon:
                return new AmazonPurchaseInfo(info);
            case ServiceType.ITunes:
                return new ItunesPurchaseInfo(info);
            case ServiceType.Spotify:
                return new SpotifyPurchaseInfo(info);
        }
    }

    public static NamesFromFilter(filter: string | undefined): string[] {
        if (!filter) {
            return [];
        }

        const ret = [];

        for (const service of enumKeys(ServiceType)) {
            const value = ServiceType[service];
            if (filter.indexOf(value) !== -1) {
                ret.push(service);
            }
        }

        return ret;
    }

    public get service(): ServiceType {
        throw new Error('Unimplemented');
    }

    public get link(): string {
        throw new Error('Unimplemented');
    }

    public get name(): string {
        throw new Error('Unimplemented');
    }

    public get logo(): string {
        return `/images/icons/${this.name}-logo.png`;
    }

    @jsonMember public albumId!: string;
    @jsonMember public songId!: string;

    public constructor(init?: Partial<PurchaseInfo>) {
        Object.assign(this, init);
    }
}

@jsonObject export class ItunesPurchaseInfo extends PurchaseInfo {
    public get service(): ServiceType {
        return ServiceType.ITunes;
    }

    public get link(): string {
        return `http://itunes.apple.com/album/id${this.albumId}?i=${this.songId}&uo=4&at=11lwtf`;
    }

    public get name(): string {
        return 'ITunes';
    }
}

@jsonObject export class AmazonPurchaseInfo extends PurchaseInfo {
    public get service(): ServiceType {
        return ServiceType.Amazon;
    }

    public get link(): string {
        return `http://www.amazon.com/gp/product/${this.cleanSong}/ref=as_li_ss_tl?ie=UTF8&camp=1789&creative=390957&creativeASIN=${this.cleanSong}&linkCode=as2&tag=msc4dnc-20`;
    }

    public get name(): string {
        return 'Amazon';
    }

    public get cleanAlbum(): string {
        return this.cleanId(this.albumId);
    }

    public get cleanSong(): string {
        return this.cleanId(this.songId);
    }

    private cleanId(id: string): string {
        if (id.startsWith('D:') || id.startsWith('A:')) {
            return id.substring(2);
        }
        return id;
    }
}

@jsonObject export class SpotifyPurchaseInfo extends PurchaseInfo {
    public get service(): ServiceType {
        return ServiceType.Spotify;
    }

    public get link(): string {
        return `http://open.spotify.com/track/${this.songId}`;
    }

    public get name(): string {
        return 'Spotify';
    }
}

@jsonObject export class PurchaseEncoded {
    @jsonMember public aa?: string;
    @jsonMember public as?: string;
    @jsonMember public ia?: string;
    @jsonMember public is?: string;
    @jsonMember public sa?: string;
    @jsonMember public ss?: string;

    public getId(service: ServiceType, obj: ServiceObjectType): string | undefined {
        return this.cleanId((this as any)[service.toLowerCase() + obj.toLowerCase()]);
    }

    public decode(): PurchaseInfo[] {
        const ret = [];

        for (const service of enumKeys(ServiceType)) {
            const purchase = this.decodeService(ServiceType[service]);
            if (purchase) {
                ret.push(purchase);
            }
        }

        return ret;
    }

    public decodeService(service: ServiceType): PurchaseInfo | undefined {
        const song = this.getId(service, ServiceObjectType.Song);
        if (!song) {
            return undefined;
        }
        const album = this.getId(service, ServiceObjectType.Album);

        return PurchaseInfo.Build(service, album, song);
    }

    private cleanId(id: string): string | undefined {
        if (!id) {
            return;
        }

        const idx = id.indexOf('[');
        if (idx > 0) {
            return id.substring(0, idx);
        }
        return id;
    }
}


