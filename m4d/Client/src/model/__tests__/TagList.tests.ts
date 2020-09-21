import 'reflect-metadata';
import { Tag } from '@/model/Tag';
import { TagList } from '../TagList';

const qualified = '+Bolero:Dance|+Latin:Music|+Nontraditional:Tempo|-Rumba:Dance|-Pop:Music';

describe('tags loading', () => {
    it ('should load taglist', () => {
        const tagList = new TagList(qualified);
        expect(tagList).toBeDefined();
        expect(tagList).toBeInstanceOf(TagList);
        expect(tagList.summary).toEqual(qualified);

        const tags = tagList.tags;
        expect(tags).toBeDefined();
        expect(tags.length).toEqual(5);
        expect(tags[0]).toBeInstanceOf(Tag);
    });

    it ('should extract adds', () => {
        const tagList = new TagList(qualified);
        const adds = tagList.Adds;

        expect(adds).toBeDefined();
        expect(adds.length).toEqual(3);
        expect(adds[0]).toBeInstanceOf(Tag);
    });

    it ('should extract removes', () => {
        const tagList = new TagList(qualified);
        const removes = tagList.Removes;

        expect(removes).toBeDefined();
        expect(removes.length).toEqual(2);
        expect(removes[0]).toBeInstanceOf(Tag);
    });

    it ('should describe adds and removes', () => {
        const tagList = new TagList(qualified);

        expect(tagList.AddsDescription)
            .toEqual('including tags Bolero, Latin and Nontraditional');
        expect(tagList.RemovesDescription)
            .toEqual('excluding tags Rumba or Pop');
    });

    it ('should describe filtered adds and removes', () => {
        const tagList = new TagList(qualified).filterCategories(['dance']);

        expect(tagList.AddsDescription)
            .toEqual('including tags Latin and Nontraditional');
        expect(tagList.RemovesDescription)
            .toEqual('excluding tag Pop');
    });

});
