import {Observable} from 'rxjs';

export default class App {

    run() {
        console.log('running...');

        Observable.interval(1000)
            .subscribe((loop) => {
                console.log('loop');
            });
    }
}
