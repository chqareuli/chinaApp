import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-coming-soon-page',
  imports: [RouterLink],
  template: `
    <section class="coming-soon">
      <div class="coming-card">
        <span class="stamp">შემდეგი ეტაპი</span>
        <p class="page-kicker">მოდული მზადდება</p>
        <h1 class="page-title">{{ title }}</h1>
        <p>{{ description }}</p>
        <a class="btn btn-secondary" routerLink="/dashboard">მთავარ გვერდზე დაბრუნება</a>
      </div>
    </section>
  `,
  styles: `
    .coming-soon { display:grid; min-height:100vh; place-items:center; padding:32px; }
    .coming-card { position:relative; width:min(100%,620px); padding:48px; overflow:hidden; border:1px solid var(--line); border-radius:var(--radius-lg); background:var(--surface); box-shadow:var(--shadow-sm); }
    .coming-card p:not(.page-kicker) { max-width:500px; margin:18px 0 28px; color:var(--ink-soft); line-height:1.75; }
    .stamp { position:absolute; top:24px; right:-38px; padding:8px 44px; color:#7b4b1e; background:#f5d8b5; font-size:.65rem; font-weight:800; transform:rotate(35deg); }
    @media (max-width:600px) { .coming-soon { align-items:start; padding:24px 16px; } .coming-card { padding:34px 24px; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComingSoonPage {
  private readonly route = inject(ActivatedRoute);
  readonly title = this.route.snapshot.data['title'] as string;
  readonly description = this.route.snapshot.data['description'] as string;
}
