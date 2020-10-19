<template>
    <a :href="link" :style="styles"><slot>{{text}}</slot></a>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';

@Component
export default class InfoLink extends Vue {
    @Prop() private text?: string;
    @Prop() private link!: string;
    @Prop() private styles?: any;
}
</script>

