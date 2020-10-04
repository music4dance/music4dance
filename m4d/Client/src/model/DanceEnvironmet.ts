/* tslint:disable:max-classes-per-file */

import 'reflect-metadata';
import { jsonMember, jsonObject, TypedJSON, jsonArrayMember} from 'typedjson';
import { DanceStats } from './DanceStats';
import { Tag } from './Tag';

TypedJSON.setGlobalConfig({
    errorHandler: (e) => {
        // tslint:disable-next-line:no-console
        console.error(e);
        throw e;
    },
});

@jsonObject class TagGroup {
    public static ToTags(groups: TagGroup[]): Tag[] {
        return groups.map((g) => g.tag);
    }

    @jsonMember public key!: string;
    @jsonMember public modified!: Date;
    @jsonMember public count?: number;
    @jsonMember public primaryId?: string;

    public get value(): string {
        const parts = this.key.split(':');
        return parts[0];
    }

    public get category(): string {
        const parts = this.key.split(':');
        return parts[1];
    }

    public get tag(): Tag {
        return new Tag({
            value: this.value,
            category: this.category,
            count: this.count ?? 0,
            primaryId: this.primaryId,
        });
    }
}

@jsonObject export class DanceEnvironment {
    @jsonArrayMember(DanceStats, {name: 'tree'}) public stats?: DanceStats[];
    @jsonArrayMember(TagGroup, { name: 'TagGroups' }) public tagGroups?: TagGroup[];

    private tagCache?: Tag[];

    public get tags(): Tag[] | undefined {
        if (!this.tagCache && this.tagGroups) {
            this.tagCache = TagGroup.ToTags(this.tagGroups);
        }
        return this.tagCache;
    }

    public fromId(id: string): DanceStats | undefined {
        return this.flatStats.find((d) => id === d.danceId);
    }

    public fromName(name: string): DanceStats | undefined {
        const n = name.toLowerCase();
        return this.flatStats.find((d) => n === d.danceName.toLowerCase());
    }

    public get flatStats(): DanceStats[] {
        return this.stats!.flatMap((group) => [group, ...group.children]);
    }
}

import environmentJson from '../assets/dance-environment.json';
let loaded: DanceEnvironment | undefined;

export function fetchEnvironment(): DanceEnvironment {
    if (!loaded) {
        let environmentString = environmentJson;
        if  ((window as any).environmentJson) {
            environmentString = (window as any).environmentJson;
        }
        loaded = TypedJSON.parse(environmentString, DanceEnvironment);
    }
    return loaded!;
}
