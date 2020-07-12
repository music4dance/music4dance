<template>
    <div>
        <dd>
            <img src="/images/icons/info.png" alt="tools" width="24" height="24" />
            Read the <a href="https://music4dance.blog">music4dance blog</a> entries about
            <a :href="tagLink">{{title}}</a>.
        </dd>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';

@Component
export default class BlogTagLink extends Vue {
    @Prop() private readonly title!: string;
    @Prop() private readonly tag!: string;

    private get icon(): string {
        return `/images/icons/info.png`;
    }

    private get tagLink(): string {
        return `https://music4dance.blog/tag/${this.tag}`;
    }
 }
</script>