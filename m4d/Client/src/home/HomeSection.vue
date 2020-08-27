<template>
    <div :id="name" style="margin-top:1rem">
        <div class="row col" style="margin-bottom:1rem">
            <img :src="image" :alt="name" width="48" height="48"/>
            <h2 :class="classes" 
                style="padding-left:.5rem; display:inline; vertical-align: center">
                {{ name }}
            </h2>
        </div>
        <slot>
            <div v-if="features" class="row col" style="display:block; margin-left:1rem">
                <feature-link v-for="feature in features" :key="feature.link" :info="feature"
                ></feature-link>
            </div>
        </slot>
    </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import FeatureLink from '../components/FeatureLink.vue';
import InfoLink from './InfoLink.vue';
import { Link } from '../model/Link';
import DanceItem from './DanceItem.vue';
import { FeatureInfo } from '@/model/FeatureInfo';

export interface CardInfo {
  title: Link;
  image: string;
  items: Link[];
}

@Component({
  components: {
    FeatureLink,
    InfoLink,
  },
})
export default class HomeSection extends Vue {
    @Prop() private name!: string;
    @Prop() private category!: string;
    @Prop() private features?: FeatureInfo[];

    private get classes(): string[] {
        return [this.category];
    }

    private get image(): string {
        return `/images/icons/${this.category}.png`;
    }
}
</script>
