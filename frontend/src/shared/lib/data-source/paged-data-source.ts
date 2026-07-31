import { Signal, computed, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

export type PagedDataSourceStatus = 'idle' | 'loading' | 'loadingMore' | 'success' | 'error';

export interface PagedDataSourcePage<TItem> {
  readonly items: readonly TItem[];
  readonly continuationToken: string | null;
}

export interface PagedDataSourceState<TItem, TQuery, TError> {
  readonly status: PagedDataSourceStatus;
  readonly items: readonly TItem[];
  readonly query: TQuery | null;
  readonly continuationToken: string | null;
  readonly error: TError | null;
}

export interface PagedDataSourceLoadRequest<TQuery> {
  readonly query: TQuery;
  readonly continuationToken: string | null;
  readonly pageSize: number;
}

export interface PagedDataSourceOptions<TItem, TQuery, TError> {
  readonly pageSize: number;
  readonly loadPage: (
    request: PagedDataSourceLoadRequest<TQuery>,
  ) => Observable<PagedDataSourcePage<TItem>>;
  readonly mapError: (error: unknown) => TError;
}

export class PagedDataSource<TItem, TQuery, TError> {
  private readonly state = signal<PagedDataSourceState<TItem, TQuery, TError>>({
    status: 'idle',
    items: [],
    query: null,
    continuationToken: null,
    error: null,
  });
  private readonly view = computed(() => this.state());

  private activeRequest: Subscription | null = null;
  private requestSequence = 0;

  constructor(private readonly options: PagedDataSourceOptions<TItem, TQuery, TError>) {}

  connect(): Signal<PagedDataSourceState<TItem, TQuery, TError>> {
    return this.view;
  }

  disconnect(): void {
    this.activeRequest?.unsubscribe();
    this.activeRequest = null;
    this.requestSequence += 1;
  }

  load(query: TQuery): void {
    this.activeRequest?.unsubscribe();
    this.state.set({
      status: 'loading',
      items: [],
      query,
      continuationToken: null,
      error: null,
    });

    this.requestPage(query, null, 'replace');
  }

  loadMore(): void {
    const current = this.state();

    if (
      current.query === null ||
      current.continuationToken === null ||
      current.status === 'loading' ||
      current.status === 'loadingMore'
    ) {
      return;
    }

    this.state.update((state) => ({
      ...state,
      status: 'loadingMore',
      error: null,
    }));

    this.requestPage(current.query, current.continuationToken, 'append');
  }

  private requestPage(
    query: TQuery,
    continuationToken: string | null,
    mode: 'replace' | 'append',
  ): void {
    const requestId = this.beginRequest();

    this.activeRequest = this.options
      .loadPage({
        query,
        continuationToken,
        pageSize: this.options.pageSize,
      })
      .subscribe({
        next: (page) => this.applyPage(page, query, requestId, mode),
        error: (error: unknown) => this.applyError(error, requestId),
      });
  }

  private applyPage(
    page: PagedDataSourcePage<TItem>,
    query: TQuery,
    requestId: number,
    mode: 'replace' | 'append',
  ): void {
    if (!this.isCurrentRequest(requestId)) {
      return;
    }

    this.state.update((state) => ({
      status: 'success',
      items: mode === 'append' ? [...state.items, ...page.items] : page.items,
      query,
      continuationToken: page.continuationToken,
      error: null,
    }));
  }

  private applyError(error: unknown, requestId: number): void {
    if (!this.isCurrentRequest(requestId)) {
      return;
    }

    this.state.update((state) => ({
      ...state,
      status: 'error',
      items: state.status === 'loadingMore' ? state.items : [],
      continuationToken: state.status === 'loadingMore' ? state.continuationToken : null,
      error: this.options.mapError(error),
    }));
  }

  private beginRequest(): number {
    this.requestSequence += 1;
    return this.requestSequence;
  }

  private isCurrentRequest(requestId: number): boolean {
    return requestId === this.requestSequence;
  }
}
