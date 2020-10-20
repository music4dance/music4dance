<template>
    <div>
        <slot v-if="loaded"></slot>
        <div v-else class="d-flex justify-content-center mb-3">
            <b-spinner label="Loading..."></b-spinner>
        </div>
    </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';

@Component
export default class Loader extends Vue {
    @Prop() private loaded!: boolean;
}
</script>
