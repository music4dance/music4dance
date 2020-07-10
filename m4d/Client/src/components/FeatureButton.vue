<template>
    <b-button
        v-if="link"
        variant="primary"
        size="sm"
        style="margin-left: 1em"
        :href="link">
        <b-icon :icon="icon"></b-icon>&nbsp;{{title}}
    </b-button>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';

@Component
export default class FeatureButton extends Vue {
    @Prop() private readonly title!: string;
    @Prop() private readonly type!: string;
    @Prop() private readonly link!: string;


    private get icon(): string {
        let icon = '';
        switch (this.type) {
            case 'play':
                icon = 'play';
                break;
            case 'docs':
                icon = 'file-text';
                break;
            case 'blog':
                icon = 'pencil-square';
            default:
                throw new Error(`${this.type} is not a valid type for Feature Button`);
        }

        return icon;
    }
 }
</script>