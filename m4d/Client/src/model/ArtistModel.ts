import { jsonObject, jsonArrayMember, jsonMember } from 'typedjson';
import { Song } from './Song';
import { SongListModel } from './SongListModel';

@jsonObject export class ArtistModel extends SongListModel {
    @jsonMember public artist!: string;
}
