<template>
    <a :href="link">{{name}}</a>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { wordsToKebab } from '../helpers/StringHelpers';

@Component
export default class LinkCategory extends Vue {
    @Prop() private readonly name!: string;

    private get link(): string {
        return `/dances/${wordsToKebab(this.name)}`;
    }
 }
</script>