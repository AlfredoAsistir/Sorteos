import { CommonModule, DOCUMENT } from '@angular/common';
import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { finalize, Subscription } from 'rxjs';
import { ReferralProgramBenefits } from '../../core/referrals/referral-program-settings.models';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { MaterialModule } from '../../material.module';
import { ReferralProgramSettingsService } from '../../services/referral-program-settings.service';
import { UserLotteryService } from '../../services/user-lottery.service';

interface FrequentlyAskedQuestion {
  question: string;
  answer: string;
}

interface FrequentlyAskedQuestionGroup {
  id: string;
  title: string;
  description: string;
  icon: string;
  searchText?: string;
  questions: readonly FrequentlyAskedQuestion[];
}

@Component({
  selector: 'app-frequently-asked-questions',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule],
  templateUrl: './frequently-asked-questions.component.html',
  styleUrl: './frequently-asked-questions.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FrequentlyAskedQuestionsComponent implements OnInit, AfterViewInit, OnDestroy {
  searchTerm = '';
  benefits: ReferralProgramBenefits | null = null;
  benefitsLoading = true;
  benefitsUnavailable = false;
  currentLottery: CurrentLottery | null = null;
  lotteryLoading = true;
  lotteryUnavailable = false;
  private fragmentSubscription = Subscription.EMPTY;
  private benefitsSubscription = Subscription.EMPTY;
  private lotterySubscription = Subscription.EMPTY;
  private scrollTimer: ReturnType<typeof setTimeout> | null = null;

  readonly groups: readonly FrequentlyAskedQuestionGroup[] = [
    {
      id: 'referidos',
      title: 'Programa de referidos',
      description: 'Invita a tus amigos y conoce todos los beneficios que puedes recibir.',
      icon: 'groups',
      questions: [
        {
          question: '¿Cómo funciona el programa de referidos?',
          answer: 'Comparte tu enlace personal de invitación. Cuando una persona se registra desde ese enlace, queda vinculada como tu referido y ambos pueden disfrutar los beneficios vigentes que se muestran a continuación.',
        },
        {
          question: '¿Qué gano si mi referido obtiene el premio principal?',
          answer: 'Cuando uno de tus referidos gana el premio principal de un sorteo y cumples las condiciones de participación aplicables, recibes el premio en efectivo (Transferencia SPEI) para referidores que esté vigente en ese sorteo.',
        },
        {
          question: '¿Puedo ganar más de una vez por el mismo referido?',
          answer: 'Sí. Si tu referido gana varios sorteos, tú también puedes recibir el premio en efectivo (Transferencia SPEI) en cada uno, siempre que cumplas las condiciones correspondientes. Revisa las condiciones de cada sorteo, ya que pueden cambiar.',
        },
        {
          question: '¿Cuánto dinero recibo si gana mi referido?',
          answer: 'El importe puede variar entre sorteos. Se toma el monto vigente al finalizar cada sorteo, por lo que debes consultar sus condiciones y requisitos de participación.',
        },
        {
          question: '¿Necesito participar en el mismo sorteo que mi referido?',
          answer: 'Algunos sorteos pueden requerir que tengas una cantidad mínima de boletos confirmados en ese mismo sorteo. Revisa los requisitos de cada sorteo, ya que pueden cambiar.',
        },
        {
          question: '¿Hay beneficios cuando mi referido deposita?',
          answer: 'Cuando el programa está activo, los depósitos confirmados que cumplan las condiciones pueden generar un bono porcentual para tu referido y para ti. El porcentaje y la cantidad máxima de depósitos bonificados dependen de las condiciones vigentes.',
        },
        {
          question: '¿Dónde consulto mis referidos y recompensas?',
          answer: 'Abre el menú de tu perfil y selecciona “Mis referidos”. Ahí encontrarás tus invitados, bonos por depósitos y premios en efectivo (Transferencia SPEI) generados por referidos ganadores.',
        },
      ],
    },
    {
      id: 'depositos',
      title: 'Depósitos y saldo',
      description: 'Agrega saldo de forma segura y úsalo para participar.',
      icon: 'account_balance_wallet',
      questions: [
        {
          question: '¿Para qué sirve realizar un depósito?',
          answer: 'Tu depósito se acredita al saldo de tu cuenta. Con ese saldo puedes comprar boletos y participar en los distintos sorteos.',
        },
        {
          question: '¿Qué métodos de depósito puedo utilizar?',
          answer: 'Puedes depositar con tarjeta, transferencia SPEI u OXXO. La pantalla de Saldo muestra las opciones disponibles y sus instrucciones.',
        },
        {
          question: '¿Cuándo se refleja mi depósito?',
          answer: 'Los pagos con tarjeta se acreditan al recibir la confirmación segura de Stripe. Los depósitos por SPEI u OXXO se actualizan automáticamente cuando Stripe confirma el pago; el tiempo depende del método utilizado.',
        },
        {
          question: '¿Dónde puedo revisar mis movimientos?',
          answer: 'En la sección Saldo encontrarás tus depósitos, compras, bonos y premios de Rascaditos, junto con el estado de cada movimiento.',
        },
      ],
    },
    {
      id: 'rascaditos',
      title: 'Rascaditos',
      description: 'Descubre tus premios y recíbelos directamente en tu saldo.',
      icon: 'auto_awesome',
      questions: [
        {
          question: '¿Cómo funcionan los Rascaditos?',
          answer: '¡Cada depósito confirmado te da nuevas oportunidades de ganar! Recibes 1 Rascadito por cada $30.00 MXN depositados; por ejemplo, un depósito de $300.00 MXN activa 10 Rascaditos. Cuando estén disponibles, puedes abrirlos, raspar sus casillas y descubrir el resultado, o utilizar el juego automático para revelar tus Rascaditos pendientes.',
        },
        {
          question: '¿Qué sucede si gano un Rascadito?',
          answer: 'El premio se acredita directamente al saldo de tu cuenta cuando el resultado queda confirmado, sin solicitudes ni trámites adicionales. Puedes utilizar ese saldo para comprar más boletos y así aumentar tus oportunidades de ganar el premio principal.',
        },
        {
          question: '¿Dónde compruebo que recibí el premio?',
          answer: 'El saldo del encabezado se actualiza y el premio aparece en tus movimientos recientes como “Premio de Rascadito”.',
        },
        {
          question: '¿Qué pasa si cierro el Rascadito o pierdo la conexión?',
          answer: 'Tu resultado permanece guardado. Puedes volver a abrirlo o reintentar de forma segura sin perder el premio obtenido.',
        },
      ],
    },
    {
      id: 'sorteos',
      title: 'Sorteos y boletos',
      description: 'Participa y consulta tus números desde tu cuenta.',
      icon: 'confirmation_number',
      questions: [
        {
          question: '¿Dónde consulto los boletos que compré?',
          answer: 'En “Mis Boletos” puedes revisar tus participaciones y los números asociados a tu cuenta.',
        },
        {
          question: '¿Cómo sé si resulté ganador?',
          answer: 'Abre “¿He ganado?” para consultar los premios relacionados con tu cuenta y el historial disponible.',
        },
        {
          question: '¿Puedo depositar mientras un sorteo está por iniciar?',
          answer: 'Por seguridad operativa, los depósitos pueden cerrarse temporalmente cuando un sorteo está próximo a iniciar o se encuentra en proceso. La pantalla de Saldo te informará cuando vuelvan a estar disponibles.',
        },
      ],
    },
    {
      id: 'transparencia',
      title: 'Boletos no vendidos: transparencia antes del sorteo',
      description: 'Conoce cómo puedes verificar los números que no se vendieron.',
      icon: 'lock',
      searchText: '75 minutos documento PDF números no vendidos ganador reprogramar primero se publica después se sortea confianza',
      questions: [],
    },
  ];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly referralProgram: ReferralProgramSettingsService,
    private readonly userLottery: UserLotteryService,
    private readonly changeDetector: ChangeDetectorRef,
    @Inject(DOCUMENT) private readonly document: Document
  ) {}

  ngOnInit(): void {
    this.benefitsSubscription = this.referralProgram.getBenefits()
      .pipe(finalize(() => {
        this.benefitsLoading = false;
        this.changeDetector.markForCheck();
      }))
      .subscribe({
        next: benefits => {
          this.benefits = benefits;
          this.benefitsUnavailable = false;
          this.changeDetector.markForCheck();
        },
        error: () => {
          this.benefitsUnavailable = true;
          this.changeDetector.markForCheck();
        },
      });

    this.lotterySubscription = this.userLottery.getCurrent()
      .pipe(finalize(() => {
        this.lotteryLoading = false;
        this.changeDetector.markForCheck();
      }))
      .subscribe({
        next: lottery => {
          this.currentLottery = lottery;
          this.lotteryUnavailable = false;
          this.changeDetector.markForCheck();
        },
        error: () => {
          this.lotteryUnavailable = true;
          this.changeDetector.markForCheck();
        },
      });
  }

  ngAfterViewInit(): void {
    this.fragmentSubscription = this.route.fragment.subscribe(fragment => {
      if (fragment) this.scheduleGroupScroll(fragment);
    });
  }

  ngOnDestroy(): void {
    this.fragmentSubscription.unsubscribe();
    this.benefitsSubscription.unsubscribe();
    this.lotterySubscription.unsubscribe();
    if (this.scrollTimer) clearTimeout(this.scrollTimer);
  }

  get filteredGroups(): readonly FrequentlyAskedQuestionGroup[] {
    const term = this.normalize(this.searchTerm.trim());
    if (!term) return this.groups;

    return this.groups
      .map(group => ({
        ...group,
        questions: group.questions.filter(item =>
          this.normalize(`${item.question} ${item.answer}`).includes(term)
        ),
      }))
      .filter(group =>
        group.questions.length > 0 ||
        this.normalize(`${group.title} ${group.description} ${group.searchText ?? ''}`).includes(term)
      );
  }

  clearSearch(): void {
    this.searchTerm = '';
  }

  get currentLotteryDateTime(): string {
    if (!this.currentLottery?.fecha) return '';

    const parts = this.currentLottery.fecha.match(
      /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/
    );
    if (!parts) return this.currentLottery.fecha;

    const months = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
      'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
    const month = months[Number(parts[2]) - 1];
    if (!month) return this.currentLottery.fecha;
    return `${Number(parts[3])} de ${month} de ${parts[1]}, ${parts[4]}:${parts[5]}`;
  }

  scrollToGroup(groupId: string): void {
    this.scheduleGroupScroll(groupId);
  }

  private scheduleGroupScroll(groupId: string): void {
    if (this.scrollTimer) clearTimeout(this.scrollTimer);
    this.scrollTimer = setTimeout(() => {
      this.scrollTimer = null;
      this.document.getElementById(groupId)?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    });
  }

  private normalize(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLocaleLowerCase('es-MX');
  }
}
