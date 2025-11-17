import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LeaveFeedback } from './leave-feedback';

describe('LeaveFeedback', () => {
  let component: LeaveFeedback;
  let fixture: ComponentFixture<LeaveFeedback>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeaveFeedback]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LeaveFeedback);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
