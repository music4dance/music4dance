/* tslint:disable:max-classes-per-file */
import 'reflect-metadata';
import { jsonObject, jsonArrayMember} from 'typedjson';
import { DanceObject } from '@/model/DanceStats';
import { Tag } from '@/model/Tag';

@jsonObject export class SearchModel {
    @jsonArrayMember(DanceObject) public dances!: DanceObject[];
    @jsonArrayMember(Tag) public tags!: Tag[];
}
