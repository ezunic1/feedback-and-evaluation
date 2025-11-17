import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FeedbackCard } from './feedback-card';

describe('FeedbackCard', () => {
  let component: FeedbackCard;
  let fixture: ComponentFixture<FeedbackCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeedbackCard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FeedbackCard);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
