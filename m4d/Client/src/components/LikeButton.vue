<template>
    <a href="#" @click.prevent="onClick" role="button">
        <b-icon-heart-fill variant="danger" v-if="state" :font-scale="scale"></b-icon-heart-fill>
        <b-iconstack v-else-if="state===false" :font-scale="scale">
            <b-icon stacked icon="heart-fill" variant="secondary" scale="0.75" shift-v="-1"></b-icon>
            <b-icon stacked icon="x-circle" variant="danger"></b-icon>
        </b-iconstack>
        <b-icon-heart variant="secondary" :font-scale="scale" v-else></b-icon-heart>
    </a>
</template>

<script lang="ts">
import 'reflect-metadata';
import axios from 'axios';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { Song, ModifiedRecord } from '@/model/Song';

interface LikeModel {
    dance?: string;
    like?: boolean;
}

@Component
export default class LikeButton extends Vue {
    @Prop() private readonly song!: Song;
    @Prop() private readonly userName!: string;
    @Prop() private readonly scale!: string;

    private get state(): boolean | undefined {
        const modified = this.userModified;
        return modified ? modified.like : undefined;
    }

    private get userModified(): ModifiedRecord | undefined {
        return this.song.getUserModified(this.userName);
    }

    private get nextState(): boolean | undefined {
        return this.rotateLike(this.state);
    }

    private setNextState(): void {
        let modified = this.userModified;
        if (modified) {
            modified.like = this.rotateLike(modified.like);
        } else {
            modified = new ModifiedRecord({ userName: this.userName, like: true});
            this.song.modifiedBy.push(modified);
        }
    }

    private async onClick(song: Song): Promise<void> {
        try {
            const newState = this.nextState;
            const response =  await axios.put(
                `/api/like/${this.song.songId}`,
                { like: newState });
            const data = response.data;
            this.setNextState();
        } catch (e) {
            // tslint:disable-next-line:no-console
            console.log(e);
            throw e;
        }
    }

    private rotateLike(like?: boolean): boolean | undefined {
        switch (like) {
            case true: return false;
            case false: return undefined;
            default: return true;
        }
    }
 }
</script>