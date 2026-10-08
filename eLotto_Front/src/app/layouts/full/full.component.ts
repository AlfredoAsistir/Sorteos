import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, OnInit, ViewChild, ViewEncapsulation, ChangeDetectionStrategy } from '@angular/core';
import { firstValueFrom, Subscription } from 'rxjs';
import { MatSidenav, MatSidenavContent } from '@angular/material/sidenav';
import { CoreService } from 'src/app/services/core.service';
import { AppSettings } from 'src/app/config';
import { filter } from 'rxjs/operators';
import { NavigationEnd, Router } from '@angular/router';
import { navItems } from './vertical/sidebar/sidebar-data';
import { AppNavItemComponent } from './vertical/sidebar/nav-item/nav-item.component';
import { RouterModule } from '@angular/router';
import { MaterialModule } from 'src/app/material.module';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from './vertical/sidebar/sidebar.component';
import { NgScrollbarModule } from 'ngx-scrollbar';
import { TablerIconsModule } from '@luoxiao123/angular-tabler-icons';
import { HeaderComponent } from './vertical/header/header.component';
import { AppHorizontalHeaderComponent } from './horizontal/header/header.component';
import { AppHorizontalSidebarComponent } from './horizontal/sidebar/sidebar.component';
import { CustomizerComponent } from './shared/customizer/customizer.component';
import { SessionService } from '../../core/auth/session.service';
import { NotificationService } from '../../core/notifications/notification.service';
import { AuthService } from '../../services/auth.service';
import { UserTopNavigationComponent } from './user-top-navigation/user-top-navigation.component';
import { AppFooterComponent } from '../shared/app-footer/app-footer.component';

const MOBILE_VIEW = 'screen and (max-width: 768px)';
const TABLET_VIEW = 'screen and (min-width: 769px) and (max-width: 1024px)';
const MONITOR_VIEW = 'screen and (min-width: 1024px)';
const BELOWMONITOR = 'screen and (max-width: 1023px)';


@Component({
    selector: 'app-full',
    imports: [
        RouterModule,
        AppNavItemComponent,
        MaterialModule,
        CommonModule,
        SidebarComponent,
        NgScrollbarModule,
        TablerIconsModule,
        HeaderComponent,
        AppHorizontalHeaderComponent,
        AppHorizontalSidebarComponent,
        CustomizerComponent,
        UserTopNavigationComponent,
        AppFooterComponent,
    ],
    templateUrl: './full.component.html',
    styleUrl: './full.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    encapsulation: ViewEncapsulation.None
})
export class FullComponent implements OnInit {
  navItems = navItems;

  @ViewChild('leftsidenav')
  public sidenav: MatSidenav;
  resView = false;
  @ViewChild('content', { static: true }) content!: MatSidenavContent;
  //get options from service
  options = this.settings.getOptions();
  private layoutChangesSubscription = Subscription.EMPTY;
  private isMobileScreen = false;
  private htmlElement!: HTMLHtmlElement;
  private appliedTheme = this.options.theme;
  private themeRevision = 0;
  private themeLoadSubscription = Subscription.EMPTY;
  private themeSaveQueue: Promise<void> = Promise.resolve();

  get isUser(): boolean {
    return this.session.roleName === 'User';
  }

  get isOver(): boolean {
    return this.isMobileScreen;
  }

  get isTablet(): boolean {
    return this.resView;
  }


  constructor(
    private settings: CoreService,
    private router: Router,
    private breakpointObserver: BreakpointObserver,
    private session: SessionService,
    private authService: AuthService,
    private notifications: NotificationService
  ) {
    this.htmlElement = document.querySelector('html')!;
    this.layoutChangesSubscription = this.breakpointObserver
      .observe([MOBILE_VIEW, TABLET_VIEW, MONITOR_VIEW, BELOWMONITOR])
      .subscribe((state) => {
        // SidenavOpened must be reset true when layout changes
        this.options.sidenavOpened = true;
        this.isMobileScreen = state.breakpoints[BELOWMONITOR];
        if (this.options.sidenavCollapsed == false) {
          this.options.sidenavCollapsed = state.breakpoints[TABLET_VIEW];
        }
        this.resView = state.breakpoints[BELOWMONITOR];
      });

    const savedTheme = this.session.snapshot?.isDark;
    if (savedTheme !== undefined) this.options.theme = savedTheme ? 'dark' : 'light';
    this.appliedTheme = this.options.theme;
    this.receiveOptions(this.options);

    // This is for scroll to top
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        if (this.router.parseUrl(this.router.url).fragment) return;
        this.content.scrollTo({ top: 0 });
      });
  }

  ngOnInit(): void {
    const token = this.session.token;
    if (!token) return;
    const revision = this.themeRevision;
    this.themeLoadSubscription = this.authService.getTheme().subscribe({
      next: ({ isDark }) => {
        if (this.session.token !== token || this.themeRevision !== revision) return;
        this.session.setTheme(isDark);
        this.applyTheme(isDark ? 'dark' : 'light');
      },
      error: () => undefined,
    });
  }

  ngOnDestroy() {
    this.layoutChangesSubscription.unsubscribe();
    this.themeLoadSubscription.unsubscribe();
  }

  toggleCollapsed() {
    this.options.sidenavCollapsed = !this.options.sidenavCollapsed;
    this.resetCollapsedState();
  }

  resetCollapsedState(timer = 400) {
    setTimeout(() => this.settings.setOptions(this.options), timer);
  }

  onSidenavClosedStart() {
  }

  onSidenavOpenedChange(isOpened: boolean) {
    this.options.sidenavOpened = isOpened;
    this.settings.setOptions(this.options);
  }

  receiveOptions(options: AppSettings): void {
    const theme = options.theme === 'dark' ? 'dark' : 'light';
    const changed = theme !== this.appliedTheme;
    this.applyTheme(theme);
    this.toggleColorsTheme(options);
    if (changed && this.session.token) {
      this.themeRevision++;
      this.saveTheme(theme, this.themeRevision);
    }
  }

  private applyTheme(theme: 'dark' | 'light'): void {
    this.options.theme = theme;
    this.settings.getOptions().theme = theme;
    this.appliedTheme = theme;
    this.toggleDarkTheme(this.options);
  }

  private saveTheme(theme: 'dark' | 'light', revision: number): void {
    const token = this.session.token;
    this.themeSaveQueue = this.themeSaveQueue.then(async () => {
      if (!token || this.session.token !== token) return;
      try {
        await firstValueFrom(this.authService.updateTheme(theme === 'dark'));
        if (this.session.token === token) this.session.setTheme(theme === 'dark');
      } catch {
        if (this.session.token !== token || revision !== this.themeRevision) return;
        this.applyTheme(this.session.snapshot?.isDark === false ? 'light' : 'dark');
        this.notifications.show(
          'No se pudo guardar el tema',
          'El tema volvió a la última preferencia guardada. Inténtalo de nuevo.',
          'warning'
        );
      }
    });
  }

  toggleDarkTheme(options: AppSettings) {
    if (options.theme === 'dark') {
      this.htmlElement.classList.add('dark-theme');
      this.htmlElement.classList.remove('light-theme');
    } else {
      this.htmlElement.classList.remove('dark-theme');
      this.htmlElement.classList.add('light-theme');
    }
  }

  toggleColorsTheme(options: AppSettings) {
    // Remove any existing theme class dynamically
    this.htmlElement.classList.forEach((className) => {
      if (className.endsWith('_theme')) {
        this.htmlElement.classList.remove(className);
      }
    });

    // Add the selected theme class
    this.htmlElement.classList.add(options.activeTheme);
  }
}
