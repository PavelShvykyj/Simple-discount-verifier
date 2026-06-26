import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-legacy-scanner-survey-redirect-page',
  templateUrl: './legacy-scanner-survey-redirect.page.html',
})
export class LegacyScannerSurveyRedirectPage implements OnInit {
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.router.navigateByUrl('/admin/service/scanner-survey', { replaceUrl: true });
  }
}
