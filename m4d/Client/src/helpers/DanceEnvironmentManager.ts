import axios from 'axios';
import { TypedJSON } from 'typedjson';
import { DanceEnvironment } from '@/model/DanceEnvironmet';

let environment: DanceEnvironment | undefined;

export async function getEnvironment(): Promise<DanceEnvironment> {
    if (environment) {
        return environment;
    }

    environment = loadFromStorage();
    if (environment) {
        return environment;
    }

    return await loadStats();
}

async function loadStats(): Promise<DanceEnvironment> {
    try {
        const response =  await axios.get(`/api/dancesstatistics/`);
        const data = response.data;
        sessionStorage.setItem('dance-stats', JSON.stringify(data));
        environment = TypedJSON.parse(data, DanceEnvironment);
        return environment!;
    } catch (e) {
        // tslint:disable-next-line:no-console
        console.log(e);
        throw e;
    }
}

function loadFromStorage(): DanceEnvironment | undefined {
    const statString = sessionStorage.getItem('dance-stats');

    if (!statString) {
        return;
    }

    environment = TypedJSON.parse(statString, DanceEnvironment);
    return environment;
}
