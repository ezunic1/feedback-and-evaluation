import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SeasonView } from './season-view';

describe('SeasonView', () => {
  let component: SeasonView;
  let fixture: ComponentFixture<SeasonView>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonView]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SeasonView);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
