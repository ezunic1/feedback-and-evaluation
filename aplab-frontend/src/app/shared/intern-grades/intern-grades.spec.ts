import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InternGrades } from './intern-grades';

describe('InternGrades', () => {
  let component: InternGrades;
  let fixture: ComponentFixture<InternGrades>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InternGrades]
    })
    .compileComponents();

    fixture = TestBed.createComponent(InternGrades);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
