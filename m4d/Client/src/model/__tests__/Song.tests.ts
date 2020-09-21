// tslint:disable: trailing-comma

import 'reflect-metadata';
import { TypedJSON } from 'typedjson';
import { SongProperty } from '../SongProperty';
import { SongHistory } from '../SongHistory';
import { Song } from '../Song';
import { ServiceType, PurchaseInfo } from '../Purchase';

const history = {
    id: 'ec118d17-5d3c-481a-9777-4fcdd087c0b1',
    properties: [
        {
            name: '.Create',
            value: ''
        },
        {
            name: 'User',
            value: 'MaggieHaggerty'
        },
        {
            name: 'Time',
            value: '10/01/2019 10:47:57'
        },
        {
            name: 'Title',
            value: 'Pick-A-Rib'
        },
        {
            name: 'Artist',
            value: 'Michael Gamble'
        },
        {
            name: 'Length',
            value: '208'
        },
        {
            name: 'User',
            value: 'batch-e'
        },
        {
            name: 'Time',
            value: '10/01/2019 10:48:18'
        },
        {
            name: 'Tempo',
            value: '183.7'
        },
        {
            name: 'Danceability',
            value: '0.65'
        },
        {
            name: 'Energy',
            value: '0.247'
        },
        {
            name: 'Valence',
            value: '0.858'
        },
        {
            name: 'Tag+',
            value: '4/4:Tempo'
        },
        {
            name: 'Sample',
            value: 'https://p.scdn.co/mp3-preview/1002783fec8075c0b3ca970a91381d7227971fb6?cid=***REMOVED***'
        },
    ]};

const song = {
    songId: 'ec118d17-5d3c-481a-9777-4fcdd087c0b1',
    tempo: 183.7,
    title: 'Pick-A-Rib',
    artist: 'Michael Gamble',
    length: 208,
    sample: 'https://p.scdn.co/mp3-preview/1002783fec8075c0b3ca970a91381d7227971fb6?cid=***REMOVED***',
    danceability: 0.65,
    energy: 0.247,
    valence: 0.858,
    created: '2019-10-01T10:47:57',
    modified: '2019-10-01T10:48:18',
    tags: [
        {
            value: '4/4',
            category: 'Tempo',
            count: 1
        },
        {
            value: 'East Coast Swing',
            category: 'Dance',
            count: 1
        },
        {
            value: 'Jazz',
            category: 'Music',
            count: 2
        },
        {
            value: 'Lindy Hop',
            category: 'Dance',
            count: 1
        }
    ],
    danceRatings: [
        {
            danceId: 'ECS',
            weight: 2
        },
        {
            danceId: 'LHP',
            weight: 2
        },
        {
            danceId: 'SWG',
            weight: 2
        }
    ],
    modifiedBy: [
        {
            userName: 'MaggieHaggerty'
        },
        {
            userName: 'batch-a'
        },
        {
            userName: 'batch-i'
        },
        {
            userName: 'batch-s'
        },
        {
            userName: 'batch-e'
        }
    ],
    albums: [
        {
            name: 'Michael Gamble & The Rhythm Serenaders',
            track: 5,
            purchase: {
                sa: '2Vk6xGoNXnY7YJlHmCWWNV',
                ss: '4MQ23wXDxF03T1FjwpHtq3[US]',
                as: 'D:B01GQZDE8C',
                aa: 'D:B01GQZD5SG',
                is: '1121446561',
                ia: '1121446386'
            }
        }
    ]
};

describe('song load from history tests', () => {
    it ('should load a simple song', () => {
        const h = TypedJSON.parse(history, SongHistory);

        expect(h).toBeDefined();
        expect(h?.id).toEqual('ec118d17-5d3c-481a-9777-4fcdd087c0b1');

        const s = Song.FromHistory(h!);
        expect(s).toBeDefined();
        expect(s?.title).toEqual('Pick-A-Rib');
        expect(s?.artist).toEqual('Michael Gamble');
        expect(s?.length).toEqual(208);
        expect(s?.tempo).toEqual(183.7);
        expect(s?.valence).toEqual(0.858);
    });
});

describe('song tests', () => {
    it ('should load a simple song', () => {
        const s = TypedJSON.parse(song, Song);

        expect(s).toBeDefined();
        expect(s).toBeInstanceOf(Song);
        expect(s?.songId).toEqual('ec118d17-5d3c-481a-9777-4fcdd087c0b1');
    });

    it ('should find purchase info', () => {
        const s = TypedJSON.parse(song, Song);

        const pi = s?.getPurchaseInfo(ServiceType.Spotify);
        expect(pi).toBeDefined();
        expect(pi).toBeInstanceOf(PurchaseInfo);
        expect(pi?.albumId).toEqual('2Vk6xGoNXnY7YJlHmCWWNV');
        expect(pi?.songId).toEqual('4MQ23wXDxF03T1FjwpHtq3');
    });

    it ('should find all purchase info', () => {
        const s = TypedJSON.parse(song, Song);

        const pis = s?.getPurchaseInfos();
        expect(pis).toBeDefined();
        expect(pis).toBeInstanceOf(Array);
        expect(pis!.length).toEqual(3);
    });
});
