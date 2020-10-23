import { jsonObject, jsonMember } from 'typedjson';
import { SongListModel } from './SongListModel';

@jsonObject export class ArtistModel extends SongListModel {
    @jsonMember public artist!: string;
}
