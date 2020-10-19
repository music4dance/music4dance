<template>
    <div>
        <b-table striped hover 
            primary-key="danceId" 
            :items="dances" :fields="fields" :filter="filter"
            :filter-function="doFilter"
            :caption="emptyTable"
            @filtered="onFiltered"
            sort-by="name"
            sort-icon-left
            responsive
            >
            <template v-slot:cell(name)="data">
                <a :href="danceLink(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:cell(groupName)="data">
                <a :href="groupLink(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:cell(mpm)="data">
                <a :href="tempoLink(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:cell(bpm)="data">
                <a :href="tempoLink(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:cell(styles)="data">
                <span v-for="(style, index) in data.item.filteredStyles(styles)" :key="style">
                    <span v-if="index !== 0">, </span>
                    <a v-if="style.indexOf(' ') !== -1" :href="styleLink(style)">
                        {{ style }}
                    </a>
                    <span v-else>
                        {{ style }}
                    </span>
                </span>
            </template>
        </b-table>
    </div>
</template>

<script lang='ts'>
import 'reflect-metadata';
import { Component, Watch, Prop, Vue } from 'vue-property-decorator';
import { flatStats } from '@/model/DanceManager';
import { DanceStats, Meter, TempoRange, DanceType, DanceFilter } from '@/model/DanceStats';
import { kebabToWords, wordsToKebab } from '@/helpers/StringHelpers';

@Component
export default class FormList extends Vue {
    @Prop() private readonly styles!: string[];
    @Prop() private readonly meters!: string[];
    @Prop() private readonly types!: string[];
    @Prop() private readonly organizations!: string[];

    @Prop() private readonly allStyles!: string[];
    @Prop() private readonly allMeters!: string[];
    @Prop() private readonly allTypes!: string[];

    private dances = flatStats().map((s) =>
        s.danceType).filter((dt) => dt && dt.groupName !== 'Performance');

    private fields = [
        {
            key: 'name',
            sortable: true,
            stickyColumn: true,
        },
        {
            key: 'meter',
            sortable: true,
            formatter: (value: Meter) => value.toString(),
        },
        {
            key: 'bpm',
            label: 'BPM',
            sortable: true,
            sortByFormatted: (value: TempoRange, key: string, item: DanceType) =>
                this.filteredTempo(item).min.toLocaleString('en', {minimumIntegerDigits: 4}),
            formatter: (value: TempoRange, key: string, item: DanceType) =>
                this.filteredTempo(item).bpm(item.meter.numerator),
        },
        {
            key: 'mpm',
            label: 'MPM',
            sortable: true,
            sortByFormatted: (value: TempoRange, key: string, item: DanceType) =>
                this.filteredTempo(item).min.toLocaleString('en', {minimumIntegerDigits: 4}),
            formatter: (value: TempoRange, key: string, item: DanceType) =>
                this.filteredTempo(item).toString(),
        },
        {
            key: 'groupName',
            label: 'Type',
            sortable: true,
        },
        {
            key: 'styles',
            sortable: 'true',
            formatter: (value: any, key: string, item: DanceType) =>
                item.filteredStyles(this.styles).join(', '),
        },
    ];

    private emptyTable: string = '';

    private get unfiltered(): string {
        const filter = {
            styles: this.allStyles,
            meters: this.allMeters,
            types: this.allTypes,
        };

        return JSON.stringify(filter);
    }

    private get filter(): DanceFilter | null {
        const filter =  {
            styles: this.styles,
            meters: this.meters,
            types: this.types,
        };

        return JSON.stringify(filter) === this.unfiltered ? null : filter;
    }

    private danceLink(dance: DanceType): string {
        return this.m4dLink(dance.seoName);
    }

    private groupLink(dance: DanceType): string {
        return this.m4dLink(dance.groupName);
    }

    private styleLink(style: string): string {
        return this.m4dLink(wordsToKebab(style));
    }

    private m4dLink(item: string): string {
        return `https://www.music4dance.net/dances/${item}`;
    }

    private tempoLink(dance: DanceType): string {
        const tempoRange = this.filteredTempo(dance);
        const numerator = dance.meter.numerator;
        return 'https://www.music4dance.net/song/?&filter=Index-' +
            `${dance.id}-Tempo-.-.-.-${tempoRange.min * numerator}-${tempoRange.max * numerator}`;
    }

    private doFilter(item: DanceType, filter: DanceFilter): boolean {
        return item.match(filter);
    }

    private onFiltered(items: DanceType[], length: number) {
        this.emptyTable = length === 0 ? 'Please select at least one item from every drop-down' : '';
    }

    private filteredTempo(dance: DanceType): TempoRange {
        const range = dance.filteredTempo(this.styles, this.organizations);
        if (range) {
            return range;
        } else {
            throw new Error(`Could not filter ${dance.name} with ${this.styles}`);
        }
    }
}
</script>
