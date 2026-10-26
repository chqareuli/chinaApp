import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { EditorIdentity } from '../../shared/editor-identity';
import { MediaService } from '../../core/services/media.service';
import { MediaAssetItem } from '../../core/models/entry.models';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-media-library',
  imports: [RouterLink, DatePipe, EditorIdentity],
  template: `<section class="panel-card">
    <header>
      <h2>MEDIA LIBRARY<br />媒体库</h2>
    </header>
    <form (submit)="$event.preventDefault(); search(query.value)">
      <input
        #query
        class="form-control"
        type="search"
        placeholder="სურათის დასახელება · 图片名称"
        aria-label="Search images · 搜索图片"
      /><button class="btn" type="submit">Search · 搜索</button>
    </form>
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
    @if (loading()) {
      <p>Loading… · 加载中</p>
    } @else {
      <table>
        <thead>
          <tr>
            <th>Author · 上传者</th>
            <th>Name · 名称</th>
            <th>Preview · 预览</th>
            <th>Entries · 词目</th>
            <th>Date · 日期</th>
            <th>Manage · 管理</th>
          </tr>
        </thead>
        <tbody>
          @for (asset of items(); track asset.id) {
            <tr>
              <td>
                <app-editor-identity
                  [id]="asset.uploadedByEditorId"
                  [name]="asset.uploadedByDisplayName"
                />
              </td>
              <td>
                <button class="image-link" type="button" (click)="open(asset, viewer)">
                  {{ asset.displayName || asset.originalFilename }}
                  <span aria-hidden="true">▧</span>
                </button>
              </td>
              <td>
                <button
                  class="image-link"
                  type="button"
                  [attr.aria-label]="'Preview ' + (asset.displayName || asset.originalFilename)"
                  (click)="open(asset, viewer)"
                >
                  @if (thumbnails()[asset.id]) {
                    <img class="thumbnail" [src]="thumbnails()[asset.id]" alt="" />
                  }
                </button>
              </td>
              <td>
                @for (entry of asset.linkedEntries ?? []; track entry.id) {
                  <a class="linked-entry" [routerLink]="['/adminpanel/entries', entry.id]">{{
                    entry.lemma
                  }}</a>
                }
              </td>
              <td>{{ asset.uploadedAtUtc | date: 'yyyy-MM-dd HH:mm' }}</td>
              <td>
                @if (asset.canManage) {
                  <button
                    class="btn"
                    type="button"
                    (click)="editAsset.set(asset); manageError.set(''); manage.showModal()"
                  >
                    Edit · 编辑
                  </button>
                }
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="6">ატვირთული სურათები არ მოიძებნა.</td>
            </tr>
          }
        </tbody>
      </table>
    }
    <footer>
      <span>{{ total() }} images · 图片</span
      ><button class="btn" [disabled]="page() <= 1 || loading()" (click)="load(page() - 1)">
        ‹</button
      ><span>{{ page() }} / {{ pages() }}</span
      ><button class="btn" [disabled]="page() >= pages() || loading()" (click)="load(page() + 1)">
        ›
      </button>
    </footer>
    <dialog
      #manage
      class="media-manage"
      aria-label="Manage image · 管理图片"
      (close)="editAsset.set(null)"
    >
      @if (editAsset(); as asset) {
        <h2>Manage image · 管理图片</h2>
        <p>Changes apply to every linked entry. · 更改将应用于所有关联词目。</p>
        <label
          >Image name · 图片名称<input
            #imageName
            class="form-control"
            [value]="asset.displayName || ''"
            maxlength="200"
        /></label>
        <button
          class="btn"
          [disabled]="managing()"
          (click)="rename(asset, imageName.value, manage)"
        >
          Save name · 保存名称
        </button>
        <hr />
        <label
          >Replace image · 替换图片<input
            #replacement
            type="file"
            accept="image/png,image/jpeg,image/webp"
        /></label>
        <button
          class="btn"
          [disabled]="managing()"
          (click)="replace(asset, replacement.files?.[0], manage)"
        >
          Replace in all entries · 替换所有关联图片
        </button>
        @if (manageError()) {
          <p role="alert">{{ manageError() }}</p>
        }
        <button class="btn" [disabled]="managing()" (click)="manage.close()">Close · 关闭</button>
      }
    </dialog>
    <dialog
      #viewer
      aria-label="Image preview · 图片预览"
      (click)="$event.target === viewer && viewer.close()"
      (close)="clearPreview()"
    >
      <button
        class="close"
        type="button"
        autofocus
        aria-label="Close · 关闭"
        (click)="viewer.close()"
      >
        ×
      </button>
      <h2>{{ selected()?.displayName || selected()?.originalFilename }}</h2>
      @if (preview()) {
        <img
          [src]="preview()"
          [alt]="selected()?.displayName || selected()?.originalFilename || ''"
        />
      } @else {
        <p>Loading… · 加载中</p>
      }
    </dialog>
  </section>`,
  styles: [
    `
      header,
      form,
      footer {
        display: flex;
        align-items: center;
        gap: 12px;
        margin-bottom: 20px;
      }
      header {
        justify-content: center;
      }
      header h2 {
        text-align: center;
        color: #adb5af;
        font-size: 1.45em;
        font-weight: 700;
      }
      .linked-entry {
        display: block;
        color: #286fac;
        margin-block: 4px;
      }
      .media-manage {
        width: min(600px, 90vw);
        box-sizing: border-box;
      }
      .media-manage label {
        display: grid;
        gap: 8px;
        margin-block: 16px;
      }
      .media-manage input {
        max-width: 100%;
        box-sizing: border-box;
      }
      .media-manage p {
        white-space: normal;
        overflow-wrap: anywhere;
      }
      .thumbnail {
        width: 90px;
        height: 65px;
        object-fit: contain;
        border-radius: 6px;
      }
      form input {
        max-width: 460px;
      }
      table {
        width: 100%;
        border-collapse: collapse;
      }
      th,
      td {
        text-align: left;
        padding: 14px 12px;
      }
      th {
        background: #edf8f3;
      }
      tbody tr:nth-child(odd) {
        background: #f3f3f5;
      }
      tbody tr:nth-child(even) {
        background: #f6fbfd;
      }
      .image-link {
        font: inherit;
        color: #286fac;
        background: none;
        border: 0;
        cursor: pointer;
        text-decoration: underline;
      }
      footer {
        justify-content: flex-end;
        margin-top: 20px;
      }
      footer > span:first-child {
        margin-right: auto;
      }
      dialog {
        border: 0;
        border-radius: 14px;
        max-width: 94vw;
        max-height: 94vh;
        padding: 36px 20px 20px;
      }
      dialog::backdrop {
        background: #14202bc4;
      }
      dialog img {
        max-width: 86vw;
        max-height: 74vh;
        object-fit: contain;
      }
      dialog h2 {
        font-size: 18px;
      }
      .close {
        position: absolute;
        right: 10px;
        top: 5px;
        font-size: 28px;
        background: none;
        border: 0;
        cursor: pointer;
      }
    `,
  ],
})
export class MediaLibrary implements OnDestroy {
  private readonly media = inject(MediaService);
  readonly editAsset = signal<MediaAssetItem | null>(null);
  readonly managing = signal(false);
  readonly manageError = signal('');
  rename(asset: MediaAssetItem, name: string, dialog: HTMLDialogElement): void {
    if (!name.trim() || this.managing()) return;
    this.managing.set(true);
    this.manageError.set('');
    this.media
      .rename(asset.id, name)
      .pipe(finalize(() => this.managing.set(false)))
      .subscribe({
        next: () => {
          dialog.close();
          this.load(this.page());
        },
        error: (error) =>
          this.manageError.set(error.error?.message || 'Could not rename image · 无法重命名图片'),
      });
  }
  replace(asset: MediaAssetItem, file: File | undefined, dialog: HTMLDialogElement): void {
    if (!file) {
      this.manageError.set('Choose an image · 请选择图片');
      return;
    }
    if (this.managing()) return;
    this.managing.set(true);
    this.manageError.set('');
    this.media
      .replace(asset.id, file)
      .pipe(finalize(() => this.managing.set(false)))
      .subscribe({
        next: () => {
          dialog.close();
          this.load(this.page());
        },
        error: (error) =>
          this.manageError.set(error.error?.message || 'Could not replace image · 无法替换图片'),
      });
  }
  readonly thumbnails = signal<Record<number, string>>({});
  private thumbnailRequests = new Subscription();
  readonly items = signal<MediaAssetItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly preview = signal('');
  readonly selected = signal<MediaAssetItem | null>(null);
  private query = '';
  private request?: Subscription;
  private imageRequest?: Subscription;
  constructor() {
    this.load(1);
  }
  pages(): number {
    return Math.max(1, Math.ceil(this.total() / 25));
  }
  search(query: string): void {
    this.query = query.trim();
    this.load(1);
  }
  load(page: number): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.thumbnailRequests.unsubscribe();
    this.thumbnailRequests = new Subscription();
    Object.values(this.thumbnails()).forEach((url) => URL.revokeObjectURL(url));
    this.thumbnails.set({});
    this.request = this.media.library(page, this.query).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.loading.set(false);
        for (const asset of result.items)
          this.thumbnailRequests.add(
            this.media.getContent(asset.storageUrl).subscribe({
              next: (blob) =>
                this.thumbnails.update((urls) => ({
                  ...urls,
                  [asset.id]: URL.createObjectURL(blob),
                })),
              error: () => {},
            }),
          );
      },
      error: () => {
        this.error.set('სურათების ჩატვირთვა ვერ მოხერხდა.');
        this.loading.set(false);
      },
    });
  }
  open(asset: MediaAssetItem, dialog: HTMLDialogElement): void {
    this.clearPreview();
    this.selected.set(asset);
    dialog.showModal();
    this.imageRequest = this.media.getContent(asset.storageUrl).subscribe({
      next: (blob) => this.preview.set(URL.createObjectURL(blob)),
      error: () => {
        dialog.close();
        this.error.set('სურათი ვერ გაიხსნა.');
      },
    });
  }
  clearPreview(): void {
    this.imageRequest?.unsubscribe();
    if (this.preview()) URL.revokeObjectURL(this.preview());
    this.preview.set('');
    this.selected.set(null);
  }
  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.thumbnailRequests.unsubscribe();
    Object.values(this.thumbnails()).forEach((url) => URL.revokeObjectURL(url));
    this.clearPreview();
  }
}
