import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ServerErrors } from './server-errors';

describe('ServerErrors', () => {
  let component: ServerErrors;
  let fixture: ComponentFixture<ServerErrors>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ServerErrors]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ServerErrors);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
