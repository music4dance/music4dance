import { jsonMember, jsonObject } from 'typedjson';

@jsonObject export class Tag {
    @jsonMember public value!: string;
    @jsonMember public category!: string;
    @jsonMember public count!: number;
}
