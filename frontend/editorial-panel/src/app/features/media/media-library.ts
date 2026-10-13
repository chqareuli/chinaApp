import { Component, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MediaService } from '../../core/services/media.service';
import { MediaAssetItem } from '../../core/models/entry.models';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-media-library', imports: [DatePipe, RouterLink],
  template: `<section class="panel-card"><header><h1>Media · 图片库</h1><a routerLink="/adminpanel/entries">All entries · 全部词目</a></header>
    <form (submit)="$event.preventDefault(); search(query.value)"><input #query class="form-control" type="search" placeholder="სურათის დასახელება · 图片名称" aria-label="Search images · 搜索图片" /><button class="btn" type="submit">Search · 搜索</button></form>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (loading()) { <p>Loading… · 加载中</p> } @else {
    <table><thead><tr><th>Author · 上传者</th><th>Name · 名称</th><th>Date · 日期</th><th>Entry · 词目</th></tr></thead><tbody>
    @for (asset of items(); track asset.id) { <tr><td>{{ asset.uploadedByDisplayName }}</td><td><button class="image-link" type="button" (click)="open(asset, viewer)">{{ asset.displayName || asset.originalFilename }} <span aria-hidden="true">▧</span></button></td><td>{{ asset.uploadedAtUtc | date:'yyyy-MM-dd HH:mm' }}</td><td><a [routerLink]="['/adminpanel/entries', asset.entryId]">↗</a></td></tr> } @empty { <tr><td colspan="4">ატვირთული სურათები არ მოიძებნა.</td></tr> }
    </tbody></table> }
    <footer><span>{{ total() }} images · 图片</span><button class="btn" [disabled]="page() <= 1 || loading()" (click)="load(page()-1)">‹</button><span>{{ page() }} / {{ pages() }}</span><button class="btn" [disabled]="page() >= pages() || loading()" (click)="load(page()+1)">›</button></footer>
    <dialog #viewer aria-label="Image preview · 图片预览" (click)="$event.target === viewer && viewer.close()" (close)="clearPreview()"><button class="close" type="button" autofocus aria-label="Close · 关闭" (click)="viewer.close()">×</button><h2>{{ selected()?.displayName || selected()?.originalFilename }}</h2>@if (preview()) { <img [src]="preview()" [alt]="selected()?.displayName || selected()?.originalFilename || ''" /> } @else { <p>Loading… · 加载中</p> }</dialog>
    </section>`,
  styles: [`header,form,footer{display:flex;align-items:center;gap:12px;margin-bottom:20px}header{justify-content:space-between}form input{max-width:460px}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:14px 12px}th{background:#edf8f3}tbody tr:nth-child(odd){background:#f3f3f5}tbody tr:nth-child(even){background:#f6fbfd}.image-link{font:inherit;color:#286fac;background:none;border:0;cursor:pointer;text-decoration:underline}footer{justify-content:flex-end;margin-top:20px}footer>span:first-child{margin-right:auto}dialog{border:0;border-radius:14px;max-width:94vw;max-height:94vh;padding:36px 20px 20px}dialog::backdrop{background:#14202bc4}dialog img{max-width:86vw;max-height:74vh;object-fit:contain}dialog h2{font-size:18px}.close{position:absolute;right:10px;top:5px;font-size:28px;background:none;border:0;cursor:pointer}`],
})
export class MediaLibrary implements OnDestroy {
  private readonly media = inject(MediaService);
  readonly items = signal<MediaAssetItem[]>([]); readonly total = signal(0); readonly page = signal(1);
  readonly loading = signal(false); readonly error = signal(''); readonly preview = signal(''); readonly selected = signal<MediaAssetItem | null>(null);
  private query = ''; private request?: Subscription; private imageRequest?: Subscription;
  constructor() { this.load(1); }
  pages(): number { return Math.max(1, Math.ceil(this.total()/25)); }
  search(query: string): void { this.query = query.trim(); this.load(1); }
  load(page: number): void {
    this.request?.unsubscribe(); this.loading.set(true); this.error.set('');
    this.request = this.media.library(page, this.query).subscribe({next: result => {this.items.set(result.items);this.total.set(result.total);this.page.set(result.page);this.loading.set(false);}, error: () => {this.error.set('სურათების ჩატვირთვა ვერ მოხერხდა.');this.loading.set(false);}});
  }
  open(asset: MediaAssetItem, dialog: HTMLDialogElement): void {
    this.clearPreview(); this.selected.set(asset); dialog.showModal();
    this.imageRequest = this.media.getContent(asset.storageUrl).subscribe({next: blob => this.preview.set(URL.createObjectURL(blob)), error: () => {dialog.close();this.error.set('სურათი ვერ გაიხსნა.');}});
  }
  clearPreview(): void {this.imageRequest?.unsubscribe();if(this.preview()) URL.revokeObjectURL(this.preview());this.preview.set('');this.selected.set(null);}
  ngOnDestroy(): void {this.request?.unsubscribe();this.clearPreview();}
}
